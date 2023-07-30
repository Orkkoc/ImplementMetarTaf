# TAF Data Processor

This is a program designed to parse XML data from Terminal Aerodrome Forecasts (TAF), and insert this data into a database.

## How it Works

The code reads XML data from a TAF, extracts the station name, and then retrieves the corresponding airport ID. If the airport ID doesn't exist, it skips the rest of the process for that station.

For each TAF, it begins a transaction and attempts to insert the TAF data into the database. It then iterates over each forecast element within the TAF, inserting these forecast elements into the database as well.

For each forecast, it retrieves the sky condition data and attempts to insert these into the database as well.

## Prerequisites

- You need an XML file containing TAF data to parse.
- The database where TAF data is stored needs to be set up with the appropriate tables and columns.

## Usage

To run the code, simply run the `TAFProcessor.cs` script in your preferred C# development environment.

Please note that this is a simplified explanation and actual implementation might require additional steps and/or code files.
