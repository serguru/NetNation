using System.Data;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;

public class Program
{

    private static string ValidateAndSplit(string row, int rowNUmber, out string[] arr)
    {
        // Ensure 'arr' is assigned before returning
        arr = Array.Empty<string>();

        // Checking the line number
        if (rowNUmber < 0)
        {
            return "rowNumber must be 0 or more";
        }

        // Prefix
        string errorMessage = $"Invalid row #{rowNUmber}: ";

        // Checking if the line filled
        if (string.IsNullOrEmpty(row))
        {
            return $"{errorMessage} empty row encountered";
        }

        // Split the row
        arr = row.Split(',');
        const int FC = 11;

        // Check fields number
        if (arr.Length != FC)
        {
            return $"{errorMessage} a row must contain exactly {FC} fields and actually {arr.Length} fields found";
        }

        for (int i = 0; i < arr.Length; i++)
        {
            arr[i] = arr[i].Trim();
        }

        // The row is valid
        return "";

    }

    private static int TryParseInt(string s)
    {
        bool valid = int.TryParse(s, out int result);
        if (valid)
        {
            return result;
        }
        ;
        throw new InvalidDataException($"Wrong integer encountered: '{s}'");
    }

    private static Guid TryParseGuid(string s)
    {
        bool valid = Guid.TryParse(s, out Guid result);
        if (valid)
        {
            return result;
        }
        ;
        throw new InvalidDataException($"Wrong integer encountered: '{s}'");
    }


    public static void Main(string[] args)
    {
        // Check input
        if (args.Length != 2)
        {
            throw new ArgumentException("Two input parameters required:\n\n1) path to data csv file and\n2) path to mapping json file\n\nExample: usagetranslator Sample_Report.csv typemap.json");
        }

        string dataPath = args[0];
        if (!File.Exists(dataPath))
        {
            throw new ArgumentException($"File '{dataPath}' not found");
        }

        string mappingPath = args[1];
        if (!File.Exists(mappingPath))
        {
            throw new ArgumentException($"File '{mappingPath}' not found");
        }

        // Read mapping file
        string jsonString = File.ReadAllText(mappingPath);
        Dictionary<string, string> mappingDictionary;
        try
        {
            mappingDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonString);

        }
        catch (Exception e)
        {
            throw new InvalidDataException($"Failed parsing {mappingPath} mapping json file with message {e.Message}");
        }

        if (mappingDictionary == null)
        {
            throw new InvalidDataException($"{mappingPath} is null");
        }


        List<string> logMessages = new List<string>();


        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        // With the following code:
        int[] excludePartnerIds = config.GetSection("ExcludePartnerIds").Get<int[]>() ?? [];


        // Create a Data Table
        DataTable table = new DataTable();
        table.Columns.Add("PartnerID", typeof(int));        // 0
        table.Columns.Add("partnerGuid", typeof(Guid));
        table.Columns.Add("accountid", typeof(int));
        table.Columns.Add("accountGuid", typeof(Guid));
        table.Columns.Add("username", typeof(string));
        table.Columns.Add("domains", typeof(string));
        table.Columns.Add("itemname", typeof(string));
        table.Columns.Add("plan", typeof(string));
        table.Columns.Add("itemType", typeof(int));
        table.Columns.Add("PartNumber", typeof(string));
        table.Columns.Add("itemCount", typeof(int));        //10

        // Read the data file, parse field values, populate the data table
        int index = 0;
        foreach (var row in File.ReadLines(dataPath))
        {
            string message = ValidateAndSplit(row, index, out string[] arr);
            if (message != "")
            {
                throw new InvalidDataException(message);
            }

            int partnerId = TryParseInt(arr[0]);

            if (index >= 1 && !excludePartnerIds.Contains(partnerId))
            {
                int itemCount = TryParseInt(arr[10]);

                if (String.IsNullOrEmpty(arr[9]))
                {
                    logMessages.Add($"Row #{index} was skipped because of missing PartNumber");
                }
                else if (itemCount <= 0)
                {
                    logMessages.Add($"Row #{index} was skipped because of non-positive itemCount");
                }
                else
                {
                    try
                    {
                        string mappedString = String.IsNullOrWhiteSpace(arr[9]) ? "" :
                            mappingDictionary[arr[9]] ?? throw new InvalidDataException($"Failed mapping '{arr[9]}', row #{index}");

                        table.Rows.Add
                            (
                                partnerId,
                                TryParseGuid(arr[1]),
                                TryParseInt(arr[2]),
                                TryParseGuid(arr[3]), // accountGuid
                                arr[4],
                                arr[5],
                                arr[6],
                                arr[7],
                                TryParseInt(arr[8]),
                                mappedString,
                                itemCount
                            );
                    }
                    catch (Exception e)
                    {
                        throw new InvalidDataException($"Error data parsing in a row {index} with message {e.Message}");
                    }
                }
            }
            index++;
        }
    }
}
