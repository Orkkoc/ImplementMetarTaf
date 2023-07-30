using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using YourProject.Domain.Entities.TAF;
using CommonServices.Services.Abstractions;
using Microsoft.Extensions.Localization;
using YourProject.Domain.Models;
using System.Threading;

namespace CommonServices.API.Hangfire
{
    public class TafJobs
    {
        private readonly ICommonServices _service;

        public TafJobs(
            ICommonServices service,
            IStringLocalizer<SharedResources> SharedResourcesLocalizer)
        {
            _service = service;
        }

        public async Task UpdateTafDataForTr()
        {

            CancellationToken cancellationToken = new CancellationToken();

            string url = $"https://www.aviationweather.gov/adds/dataserver_current/httpparam?dataSource=tafs&requestType=retrieve&format=xml&stationString=~TR&hoursBeforeNow=1";

            using var httpClient = new HttpClient();

            HttpResponseMessage response;
            try
            {
                response = await httpClient.GetAsync(url, cancellationToken);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to fetch TAF data: {e.Message}");
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine("Failed to fetch TAF data");
                return;
            }

            string data;
            try
            {
                data = await response.Content.ReadAsStringAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to read TAF data: {e.Message}");
                return;
            }

            XDocument doc;
            try
            {
                doc = XDocument.Parse(data);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to parse TAF data: {e.Message}");
                return;
            }

            List<XElement> tafs = doc.Root.Elements("data").Elements("TAF").ToList();
            await _service.BeginTransactionAsync(cancellationToken);
            //Add new data to TAF History
            foreach (var tafhistory in tafs)
            {

                var stationName = tafhistory.Element("station_id").Value;
                var airportId = await _service.AirportService.GetAirportIdByICAO(stationName);
                if (airportId == 0)
                {
                    Console.WriteLine("AirportId doesn't exist. Skipping TAF History processing.");
                    continue; // Skip to the next iteration
                }
                var tafHistoryResult = await InsertTafHistory(tafhistory, stationName, airportId, cancellationToken);
                await _service.CompleteAsync(cancellationToken);
            }
            await _service.CommitTransactionAsync(cancellationToken);
            try
            {
                await _service.BeginTransactionAsync(cancellationToken);
                var tafDeleteResult = await DeleteAllTAFs(cancellationToken);
                await _service.CommitTransactionAsync(cancellationToken);


            }
            catch (Exception e)
            {

                Console.WriteLine($"Failed to delete TAF data: {e.Message}");

                await _service.RollbackTransactionAsync(cancellationToken);
            }

            foreach (var taf in tafs)
            {
                try
                {

                    var stationName = taf.Element("station_id").Value;
                    var airportId = await _service.AirportService.GetAirportIdByICAO(stationName);
                    if (airportId == 0)
                    {
                        Console.WriteLine("AirportId doesn't exist. Skipping TAF processing.");
                        continue; // Skip to the next iteration
                    }
                    await _service.BeginTransactionAsync(cancellationToken);

                    var tafResult = await InsertTaf(taf, stationName, airportId, cancellationToken);

                    await _service.CompleteAsync(cancellationToken);
                    var tafId = tafResult.ReturnValue.Id;
                    var tafName= tafResult.ReturnValue.Name;

                    // Parse the XML data into a list of forecast elements
                    List<XElement> forecastElements = taf.Elements("forecast").ToList();
                    foreach (var forecastElement in forecastElements)
                    {
                        var forecastResult = await InsertTafForecast(tafId, tafName, forecastElement, cancellationToken);

                        await _service.CompleteAsync(cancellationToken);
                        var forecastId = forecastResult.ReturnValue.Id;

                        // TAFSkyConditions
                        List<XElement> skyConditionElements = forecastElement.Elements("sky_condition").ToList();
                        foreach (var skyConditionElement in skyConditionElements)
                        {
                            // Save sky conditions to the database

                            var skyConditionResponse = await InsertTafSkyCondition(forecastId, skyConditionElement, cancellationToken);
                            if (!skyConditionResponse.isSuccess)
                            {
                                Console.WriteLine(skyConditionResponse.Message);
                            }
                            await _service.CompleteAsync(cancellationToken);
                        }

                    }
                    await _service.CompleteAsync(cancellationToken);
                    await _service.CommitTransactionAsync(cancellationToken);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to insert TAF data: {e.Message}");

                    await _service.RollbackTransactionAsync(cancellationToken);
                }
            }
        }

        public async Task<GenericResponse<TAF>> InsertTaf(XElement taf,string stationName, long airportId, CancellationToken cancellationToken)
        {
            TAF model = new TAF();

            model.Name = stationName;
            model.IssueTime = DateTime.Parse(taf.Element("issue_time").Value);
            model.BulletinTime = DateTime.Parse(taf.Element("bulletin_time").Value);
            model.ValidTimeFrom = DateTime.Parse(taf.Element("valid_time_from").Value);
            model.ValidTimeTo = DateTime.Parse(taf.Element("valid_time_to").Value);
            model.AirportId = airportId;
            model.ElevationM = float.Parse(taf.Element("elevation_m").Value);
            model.Latitude = float.Parse(taf.Element("latitude").Value);
            model.Longitude = float.Parse(taf.Element("longitude").Value);
            model.RawText = taf.Element("raw_text").Value;
            model.IsActive = true;
            var serviceResponse = await _service.TAFService.CreateAsync(model, cancellationToken);
            if (!serviceResponse.isSuccess)
            {
                Console.WriteLine(serviceResponse.Message);
            }
            return serviceResponse;

        }

        public async Task<GenericResponse<TAFForecast>> InsertTafForecast(long tafId, string tafName, XElement forecastElement, CancellationToken cancellationToken)
        {
            TAFForecast forecast = new TAFForecast();

            forecast.TafId = tafId;
            forecast.Name = tafName;
            forecast.FcstTimeFrom = forecastElement.Element("fcst_time_from") != null ? DateTimeOffset.Parse(forecastElement.Element("fcst_time_from").Value) : null;
            forecast.FcstTimeTo = forecastElement.Element("fcst_time_to") != null ? DateTimeOffset.Parse(forecastElement.Element("fcst_time_to").Value) : null;
            forecast.WindDirDegrees = forecastElement.Element("wind_dir_degrees") != null ? int.Parse(forecastElement.Element("wind_dir_degrees").Value) : 0;
            forecast.WindSpeedKt = forecastElement.Element("wind_speed_kt") != null ? int.Parse(forecastElement.Element("wind_speed_kt").Value) : 0;
            forecast.VisibilityStatuteMi = forecastElement.Element("visibility_statute_mi") != null ? double.Parse(forecastElement.Element("visibility_statute_mi").Value) : 0.0;
            forecast.ChangeIndicator = forecastElement.Element("change_indicator") != null ? forecastElement.Element("change_indicator").Value : string.Empty;
            forecast.TimeBecoming = forecastElement.Element("time_becoming") != null ? DateTimeOffset.Parse(forecastElement.Element("time_becoming").Value) : null;
            forecast.WxString = forecastElement.Element("wx_string") != null ? forecastElement.Element("wx_string").Value : string.Empty;
            forecast.IsActive = true;
            var serviceResponse = await _service.TAFForecastService.CreateAsync(forecast, cancellationToken);

            if (!serviceResponse.isSuccess)
            {
                Console.WriteLine(serviceResponse.Message);
            }

            return serviceResponse;
        }

        public async Task<GenericResponse<TAFSkyCondition>> InsertTafSkyCondition(long forecastId, XElement skyConditionElement, CancellationToken cancellationToken)
        {
            TAFSkyCondition skyCondition = new TAFSkyCondition();
            skyCondition.TAFForecastId = forecastId;
            skyCondition.Name = skyConditionElement.Attribute("sky_cover") != null ? skyConditionElement.Attribute("sky_cover").Value : string.Empty;
            long tempCloudBaseFtAgl;
            skyCondition.CloudBaseFtAgl = skyConditionElement.Attribute("cloud_base_ft_agl") != null && long.TryParse(skyConditionElement.Attribute("cloud_base_ft_agl").Value, out tempCloudBaseFtAgl)? tempCloudBaseFtAgl : (long?)null;
            skyCondition.IsActive = true;

            var serviceResponse = await _service.TAFSkyConditionService.CreateAsync(skyCondition, cancellationToken);

            if (!serviceResponse.isSuccess)
            {
                Console.WriteLine(serviceResponse.Message);
            }

            return serviceResponse;
        }

        public async Task<GenericResponse<TAFHistory>> InsertTafHistory(XElement taf, string stationName, long airportId, CancellationToken cancellationToken)
        {
            TAFHistory model = new TAFHistory();

            model.Name = stationName;
            model.AirportId = airportId;
            model.RawText = taf.Element("raw_text").Value;
            model.IsActive = true;
            var serviceResponse = await _service.TAFHistoryService.CreateAsync(model, cancellationToken);
            if (!serviceResponse.isSuccess)
            {
                Console.WriteLine(serviceResponse.Message);
            }
            return serviceResponse;

        }

        public async Task<bool> DeleteAllTAFs(CancellationToken cancellationToken)
        {
            try
            {
                //Delete old data

                await _service.TAFSkyConditionService.DeleteAllAsync(cancellationToken);
                await _service.CompleteAsync(cancellationToken);
                await _service.TAFForecastService.DeleteAllAsync(cancellationToken);
                await _service.CompleteAsync(cancellationToken);
                await _service.TAFService.DeleteAllAsync(cancellationToken);
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

        public async Task<int> InsertOldDataToTAFHistory(CancellationToken cancellationToken)
        {
            // Get all records from the TAFs table
            var tafs = _service.TAFService.GetAll();

            int numInserted = 0;
            foreach (var taf in tafs)
            {
                try
                {
                    await _service.BeginTransactionAsync(cancellationToken);
                    // Create a new TAFHistory from each TAF
                    var tafHistory = new TAFHistory
                    {
                        Name = taf.Name,
                        AirportId = taf.AirportId,
                        Airport = taf.Airport,
                        RawText = taf.RawText,
                        IsActive = taf.IsActive,
                        DeactivationDate = taf.DeactivationDate
                    };

                    // Insert the TAFHistory
                    var response = await _service.TAFHistoryService.CreateAsync(tafHistory, cancellationToken);
                    await _service.CompleteAsync(cancellationToken);

                    if (response.isSuccess)
                    {
                        numInserted++;
                        await _service.CommitTransactionAsync(cancellationToken);
                    }
                    else
                    {
                        await _service.RollbackTransactionAsync(cancellationToken);
                        Console.WriteLine(response.Message);
                    }
                }
                catch (Exception e)
                {
                    await _service.RollbackTransactionAsync(cancellationToken);
                    Console.WriteLine($"Failed to insert TAF History data: {e.Message}");
                }
            }

            try
            {
                //Delete old data
                await _service.BeginTransactionAsync(cancellationToken);

                await _service.TAFSkyConditionService.DeleteAllAsync(cancellationToken);
                await _service.CompleteAsync(cancellationToken);
                await _service.TAFForecastService.DeleteAllAsync(cancellationToken);
                await _service.CompleteAsync(cancellationToken);
                await _service.TAFService.DeleteAllAsync(cancellationToken);
                await _service.CompleteAsync(cancellationToken);

                await _service.CommitTransactionAsync(cancellationToken);
            }
            catch (Exception e)
            {
                await _service.RollbackTransactionAsync(cancellationToken);
                Console.WriteLine($"Failed to delete data: {e.Message}");
            }

            return numInserted;
        }
    }
}
