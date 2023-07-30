# TAF and METAR Data Processing Services

This project contains two classes, `TafJobs` and `MetarJobs`, that fetch and process aviation weather data in TAF (Terminal Aerodrome Forecast) and METAR (Meteorological Aerodrome Report) format, respectively.

## Getting Started

These instructions will help you to run this project on your local machine.

### Prerequisites

- .NET 5.0 or above
- Create a service for implementing as `IServices` interface

### Running the Code

1. Clone the repository.
2. Ensure your implementation of `IServices` is properly configured and injected into the `TafJobs` and `MetarJobs` classes.
3. Call `UpdateTafDataForTr()` method from the `TafJobs` class and `UpdateMetarDataForTr()` method from the `MetarJobs` class to fetch and process the TAF and METAR data, respectively.

## Classes

### TafJobs

This class fetches TAF data from the aviationweather.gov API, parses the XML response, and inserts the data into a database. The following steps describe the main actions of the `UpdateTafDataForTr()` method:

1. Fetches TAF data from the aviationweather.gov API.
2. Parses the XML response.
3. Starts a database transaction.
4. Inserts each TAF report into the database.
5. For each forecast within a TAF, inserts the forecast into the database.
6. For each sky condition within a forecast, inserts the sky condition into the database.
7. Commits the database transaction.
8. If an error occurs at any point, the transaction is rolled back.

### MetarJobs

This class fetches METAR data from the aviationweather.gov API, parses the XML response, and inserts the data into a database. The following steps describe the main actions of the `UpdateMetarDataForTr()` method:

1. Fetches METAR data from the aviationweather.gov API.
2. Parses the XML response.
3. Starts a database transaction.
4. Inserts each METAR report into the Metar History.
5. Deletes all METARs.
6. For each METAR, inserts it into the database.
7. For each sky condition within a METAR, inserts the sky condition into the database.
8. Commits the database transaction.
9. If an error occurs at any point, the transaction is rolled back.

## Authors

Orkun Kocatürk - https://github.com/Orkkoc

## License

This project is licensed under the MIT License - see the [LICENSE.md](LICENSE.md) file for details
