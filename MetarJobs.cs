using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using YourNameSpace.Domain.Entities.Metar;
using YourNameSpace.Services.Abstractions;
using Microsoft.Extensions.Localization;
using System.Threading;
using YourNameSpace.Domain.Models;

namespace YourNameSpace.API.Hangfire
{
    public class MetarJobs
    {
        private readonly IServices _service;

        public MetarJobs(
            IServices service,
            IStringLocalizer<SharedResources> SharedResourcesLocalizer)
        {
            _service = service;
        }

        public async Task UpdateMetarDataForTr()
        {
            CancellationToken cancellationToken = new CancellationToken();

            string url = $"https://www.aviationweather.gov/adds/dataserver_current/httpparam?dataSource=metars&requestType=retrieve&format=xml&stationString=~TR&hoursBeforeNow=1";

            using var httpClient = new HttpClient();

            HttpResponseMessage response;
            try
            {
                response = await httpClient.GetAsync(url, cancellationToken);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to fetch METAR data: {e.Message}");
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine("Failed to fetch METAR data");
                return;
            }

            string data;
            try
            {
                data = await response.Content.ReadAsStringAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to read METAR data: {e.Message}");
                return;
            }

            XDocument doc;
            try
            {
                doc = XDocument.Parse(data);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to parse METAR data: {e.Message}");
                return;
            }

            List<XElement> metars = doc.Root.Elements("data").Elements("METAR").ToList();
            await _service.BeginTransactionAsync(cancellationToken);
            //Add new data to Metar History
            foreach (var metarhistory in metars)
            {

                var stationName = metarhistory.Element("station_id").Value;
                var airportId = await _service.AirportService.GetAirportIdByICAO(stationName);
                if (airportId == 0)
                {
                    Console.WriteLine("AirportId doesn't exist. Skipping Metar History processing.");
                    continue; // Skip to the next iteration
                }
                var metarHistoryResult = await InsertMetarHistory(metarhistory, stationName, airportId, cancellationToken);
                await _service.CompleteAsync(cancellationToken);
            }
            await _service.CommitTransactionAsync(cancellationToken);
            try
            {
                await _service.BeginTransactionAsync(cancellationToken);
                var metarDeleteResult = await DeleteAllMetars(cancellationToken);
                await _service.CommitTransactionAsync(cancellationToken);


            }
            catch (Exception e)
            {

                Console.WriteLine($"Failed to delete Metar data: {e.Message}");

                await _service.RollbackTransactionAsync(cancellationToken);
            }
            foreach (var metar in metars)
            {
                try
                {
                    var stationName = metar.Element("station_id").Value;
                    var airportId = await _service.AirportService.GetAirportIdByICAO(stationName);
                    if (airportId == 0)
                    {
                        Console.WriteLine("AirportId doesn't exist. Skipping Metar processing.");
                        continue; // Skip to the next iteration
                    }

                    await _service.BeginTransactionAsync(cancellationToken);
                    var metarResult = await InsertMetar(metar, stationName, airportId, cancellationToken);
                    await _service.CompleteAsync(cancellationToken);
                    var metarId = metarResult.ReturnValue.Id;

                    // Parse the XML data into a list of sky_condition elements
                    List<XElement> skyConditionElements = metar.Elements("sky_condition").ToList();
                    foreach (var skyConditionElement in skyConditionElements)
                    {
                        if (skyConditionElement.Attribute("sky_cover") == null)
                        {
                            Console.WriteLine("Sky_cover doesn't exist. Skipping Metar SkyCondition processing.");
                            continue; // Skip to the next iteration
                        }
                        // Save sky conditions to the database
                        var skyConditionResponse = await InsertMetarSkyCondition(metarId, skyConditionElement, cancellationToken);
                        if (!skyConditionResponse.isSuccess)
                        {
                            Console.WriteLine(skyConditionResponse.Message);
                        }
                        await _service.CompleteAsync(cancellationToken);
                    }
                    await _service.CompleteAsync(cancellationToken);
                    await _service.CommitTransactionAsync(cancellationToken);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to insert METAR data: {e.Message}");

                    await _service.RollbackTransactionAsync(cancellationToken);
                }
            }
        }

        public async Task<GenericResponse<Metar>> InsertMetar(XElement metar, string stationName, long airportId, CancellationToken cancellationToken)
        {
            Metar model = new Metar();

            model.Name = stationName;
            model.AirportId = airportId;
            model.ObservationTime = metar.Element("observation_time") != null ? DateTime.Parse(metar.Element("observation_time").Value) : default;
            model.TempC = metar.Element("temp_c") != null ? float.Parse(metar.Element("temp_c").Value) : default;
            model.DewpointC = metar.Element("dewpoint_c") != null ? float.Parse(metar.Element("dewpoint_c").Value) : default;
            model.WindDirDegrees = metar.Element("wind_dir_degrees") != null ? long.Parse(metar.Element("wind_dir_degrees").Value) : default;
            model.WindSpeedKt = metar.Element("wind_speed_kt") != null ? long.Parse(metar.Element("wind_speed_kt").Value) : default;
            model.VisibilityStatuteMi = metar.Element("visibility_statute_mi") != null ? float.Parse(metar.Element("visibility_statute_mi").Value) : default;
            model.AltimInHg = metar.Element("altim_in_hg") != null ? float.Parse(metar.Element("altim_in_hg").Value) : default;
            model.FlightCategory = metar.Element("flight_category") != null ? metar.Element("flight_category").Value : null;
            model.MetarType = metar.Element("metar_type") != null ? metar.Element("metar_type").Value : null;
            model.ElevationM = metar.Element("elevation_m") != null ? float.Parse(metar.Element("elevation_m").Value) : default;
            model.RawText = metar.Element("raw_text") != null ? metar.Element("raw_text").Value : null;
            model.IsActive = true;
            model.NoSignal = metar.Element("quality_control_flags")?.Element("no_signal")?.Value.ToUpper() == "TRUE";

            var serviceResponse = await _service.MetarService.CreateAsync(model, cancellationToken);

            if (!serviceResponse.isSuccess)
            {
                Console.WriteLine(serviceResponse.Message);
            }

            return serviceResponse;
        }

        public async Task<GenericResponse<MetarSkyCondition>> InsertMetarSkyCondition(long metarId, XElement skyConditionElement, CancellationToken cancellationToken)
        {
            MetarSkyCondition skyCondition = new MetarSkyCondition();


            skyCondition.MetarId = metarId;

            skyCondition.Name = skyConditionElement.Attribute("sky_cover").Value;
            var cloudBaseFtAglAttr = skyConditionElement.Attribute("cloud_base_ft_agl");
            if (cloudBaseFtAglAttr != null && long.TryParse(cloudBaseFtAglAttr.Value, out long cloudBaseFtAglParsed))
            {
                skyCondition.CloudBaseFtAgl = cloudBaseFtAglParsed;
            }
            else
            {
                skyCondition.CloudBaseFtAgl = null;
            }

            var serviceResponse = await _service.MetarSkyConditionService.CreateAsync(skyCondition, cancellationToken);

            if (!serviceResponse.isSuccess)
            {
                Console.WriteLine(serviceResponse.Message);
            }

            return serviceResponse;
        }

        public async Task<GenericResponse<MetarHistory>> InsertMetarHistory(XElement metar, string stationName, long airportId, CancellationToken cancellationToken)
        {
            MetarHistory model = new MetarHistory();

            model.Name = stationName;
            model.AirportId = airportId;
            model.RawText = metar.Element("raw_text").Value;
            model.IsActive = true;
            var serviceResponse = await _service.MetarHistoryService.CreateAsync(model, cancellationToken);
            if (!serviceResponse.isSuccess)
            {
                Console.WriteLine(serviceResponse.Message);
            }
            return serviceResponse;

        }

        public async Task<bool> DeleteAllMetars(CancellationToken cancellationToken)
        {
            try
            {
                //Delete old data

                await _service.MetarSkyConditionService.DeleteAllAsync(cancellationToken);
                await _service.CompleteAsync(cancellationToken);
                await _service.MetarService.DeleteAllAsync(cancellationToken);
                await _service.CompleteAsync(cancellationToken);

            }
            catch (Exception e)
            {
                await _service.RollbackTransactionAsync(cancellationToken);
                Console.WriteLine($"Failed to delete data: {e.Message}");
                return false;
            }
            return true;
        }

    }
}
