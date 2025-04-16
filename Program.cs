using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;
using System.Text;

public class Program
{
    #region helpers
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

    private static string Guid2Str(Guid guid)
    {
        // Leave alphanumeric only
        string s = Regex.Replace(guid.ToString(), "[^a-zA-Z0-9]", "");
        if (s.Length == 32)
        {
            return s;
        }
        if (s.Length > 32)
        {
            return s.Substring(32);
        }
        return s.PadRight(32, '0');
    }

    static Dictionary<string, int> countToUsage = new Dictionary<string, int>()
        {
            { "EA000001GB0O", 1000 },
            { "PMQ00005GB0R", 5000 },
            { "SSX006NR", 1000 },
            { "SPQ00001MB0R", 2000 }
        };


    // itemCount converter
    public static int ConvertItemCount(string partNumber, int itemCount)
    {

        if (!countToUsage.ContainsKey(partNumber))
        {
            return itemCount;
        }

        return itemCount / countToUsage[partNumber];

    }

    #endregion

    public static void Main(string[] args)
    {
        if (args.Length == 0 || args[0].Equals("help", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("This program is for converting csv data into two insert SQL statemerts.");
            Console.WriteLine("Command: NetNationExercise <path-to-csv-file> <path-to-mapping-json-file>.");
            return;
        }

        Console.WriteLine("Working...");

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
        Dictionary<string, string> mappingDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonString)
                            ?? throw new InvalidDataException($"Failed to deserialize {mappingPath} mapping json file. The result is null.");

        List<string> logMessages = new List<string>();

        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";

        // Build configuration
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        int[] excludePartnerIds = config.GetSection("ExcludePartnerIds").Get<int[]>() ?? [];

        StringBuilder sbChargeable = new StringBuilder("insert into chargeable (partnerID, product, partnerPurchasedPlanID, [plan], usage) values\n");

        // A collection to build the 'domains' insert statement
        Dictionary<string, string> domains = new();

        // constants
        const string comma = ",";
        const string singleQuote = "'";


        // Read the data file, parse field values, append sbChargeable
        int index = -1;
        foreach (var row in File.ReadLines(dataPath))
        {
            index++;

            string message = ValidateAndSplit(row, index, out string[] arr);
            if (message != "")
            {
                throw new InvalidDataException(message);
            }

            if (index < 1)
            {
                continue;
            }

            int partnerId = TryParseInt(arr[0]);
            if (excludePartnerIds.Contains(partnerId))
            {
                continue;
            }

            int itemCount = TryParseInt(arr[10]);

            if (String.IsNullOrEmpty(arr[9]))
            {
                logMessages.Add($"Row #{index} was skipped because of missing PartNumber");
            }
            else if (!mappingDictionary.ContainsKey(arr[9]))
            {
                logMessages.Add($"Row #{index} was skipped because of missing PartNumber '{arr[9]}'");
            }
            else if (itemCount <= 0)
            {
                logMessages.Add($"Row #{index} was skipped because of non-positive itemCount");
            }
            else
            {
                string partnerPurchasedPlanID = Guid2Str(TryParseGuid(arr[3]));
                string domain = arr[5];
                try
                {
                    sbChargeable.Append("(");
                    sbChargeable.Append(partnerId);
                    sbChargeable.Append(comma);
                    sbChargeable.Append(singleQuote);
                    sbChargeable.Append(mappingDictionary[arr[9]]);
                    sbChargeable.Append(singleQuote);
                    sbChargeable.Append(comma);
                    sbChargeable.Append(singleQuote);
                    sbChargeable.Append(partnerPurchasedPlanID);
                    sbChargeable.Append(singleQuote);
                    sbChargeable.Append(comma);
                    sbChargeable.Append(TryParseInt(arr[8]));
                    sbChargeable.Append(comma);
                    sbChargeable.Append(ConvertItemCount(arr[9], itemCount));
                    sbChargeable.Append("),\n");


                    if (!domains.ContainsKey(domain))
                    {
                        domains.Add(domain, partnerPurchasedPlanID);
                    }
                }
                catch (Exception e)
                {
                    throw new InvalidDataException($"Error data parsing in a row {index} with message {e.Message}");
                }
            }
        }

        sbChargeable.Remove(sbChargeable.Length - 2, 2);
        sbChargeable.Append(";");
        string insertChargeable = sbChargeable.ToString();




        StringBuilder sbDomains = new StringBuilder("insert into domains (domain, partnerPurchasedPlanID) values\n");

        foreach (var item in domains)
        {
            sbDomains.Append("('");
            sbDomains.Append(item.Key);
            sbDomains.Append(singleQuote);
            sbDomains.Append(comma);
            sbDomains.Append(singleQuote);
            sbDomains.Append(item.Value);
            sbDomains.Append("'),\n");
        }
        sbDomains.Remove(sbDomains.Length - 2, 2);
        sbDomains.Append(";");
        string insertDomains = sbDomains.ToString();


        const string s1 = "insert-chargeable.sql";
        const string s2 = "insert-domains.sql";
        const string s3 = "log-errors.txt";

        File.WriteAllText(s1, insertChargeable);
        File.WriteAllText(s2, insertDomains);
        File.WriteAllLines(s3, logMessages);

        Console.WriteLine($"Insert statements generated. Please see files {s1} and {s2}.");
        Console.WriteLine($"Please find error messages in {s3}");
    }
}


