using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;
using System.Text;

public class Program
{
    #region helpers

    /// <summary>
    /// Validatates string and splits it to an array
    /// </summary>
    /// <param name="row"></param>
    /// <param name="rowNUmber"></param>
    /// <param name="arr"></param>
    /// <returns></returns>
    public static string ValidateAndSplit(string row, int rowNUmber, out string[] arr)
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

    /// <summary>
    /// Converts nullable string to int?
    /// </summary>
    /// <param name="s"></param>
    /// <returns></returns>
    /// <exception cref="InvalidDataException"></exception>
    public static int? TryParseInt(string? s)
    {
        if (String.IsNullOrEmpty(s))
        {
            return null;
        }

        bool valid = int.TryParse(s, out int result);
        if (valid)
        {
            return result;
        }
        ;
        throw new InvalidDataException($"Wrong integer encountered: '{s}'");
    }

    /// <summary>
    /// Converts nullable string to Guid?
    /// </summary>
    /// <param name="s"></param>
    /// <returns></returns>
    /// <exception cref="InvalidDataException"></exception>
    public static Guid? TryParseGuid(string? s)
    {
        if (String.IsNullOrEmpty(s))
        {
            return null;
        }
        bool valid = Guid.TryParse(s, out Guid result);
        if (valid)
        {
            return result;
        }
        ;
        throw new InvalidDataException($"Wrong Guid encountered: '{s}'");
    }

    /// <summary>
    /// Converts nullable Guid to string?
    /// </summary>
    /// <param name="guid"></param>
    /// <returns></returns>
    public static string? Guid2Str(Guid? guid)
    {
        if (!guid.HasValue)
        {
            return null;
        }
        // Leave alphanumeric only
        string s = Regex.Replace(guid.Value.ToString(), "[^a-zA-Z0-9]", "");
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

    public static Dictionary<string, int> countToUsage = new()
    {
        { "EA000001GB0O", 1000 },
        { "PMQ00005GB0R", 5000 },
        { "SSX006NR", 1000 },
        { "SPQ00001MB0R", 2000 }
    };


    /// <summary>
    /// Converts itemCount to usage
    /// </summary>
    /// <param name="partNumber"></param>
    /// <param name="itemCount"></param>
    /// <returns></returns>
    public static int? ConvertItemCount(string? partNumber, int? itemCount)
    {
        if (String.IsNullOrEmpty(partNumber))
        {
            return null;
        }
        if (!itemCount.HasValue)
        {
            return null;
        }
        if (!countToUsage.ContainsKey(partNumber.ToUpper()))
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

        StringBuilder sbChargeable = new StringBuilder("INSERT INTO chargeable (partnerID, product, partnerPurchasedPlanID, [plan], usage) VALUES\n");

        // A collection to build the 'domains' insert statement
        Dictionary<string, string?> domains = new();

        // constants
        const string comma = ",";
        const string singleQuote = "'";
        const string nullString = "NULL";

        // Read the data file, parse field values, append sbChargeable

        /*
            0: PartnerID    -> partnerID,        
            1: partnerGuid,
            2: accountid,
            3: accountGuid  -> partnerPurchasedPlanID,
            4: username,
            5: domains,
            6: itemname,
            7: plan         -> plan,
            8: itemType,
            9: PartNumber   -> product
           10: itemCount    -> usage
        */

        var runningCounts = new List<(int RowNo, string Product, int ItemCount, int RunningTotal)>();

        int index = 0;
        foreach (var row in File.ReadLines(dataPath))
        {
            index++;

            string message = ValidateAndSplit(row, index, out string[] arr);
            if (message != "")
            {
                throw new InvalidDataException(message);
            }

            if (index == 1)
            {
                continue;
            }

            // Excluding rows for patherIds included into
            // "ExcludePartnerIds" section in appsettings.json
            int? partnerId = TryParseInt(arr[0]);
            if (partnerId.HasValue && excludePartnerIds.Contains(partnerId.Value))
            {
                continue;
            }

            if (String.IsNullOrEmpty(arr[9]))
            {
                logMessages.Add($"Row #{index} was skipped because of missing PartNumber");
                continue;
            }

            if (!mappingDictionary.ContainsKey(arr[9]))
            {
                logMessages.Add($"Row #{index} was skipped because of missing PartNumber '{arr[9]}'");
                continue;
            }

            int? itemCount = TryParseInt(arr[10]);

            if (itemCount <= 0)
            {
                logMessages.Add($"Row #{index} was skipped because of non-positive itemCount");
                continue;
            }

            string partnerIdStr = partnerId.HasValue ? partnerId.Value.ToString() : nullString;

            string? partnerPurchasedPlanID = Guid2Str(TryParseGuid(arr[3]));

            int? usage = ConvertItemCount(arr[10], itemCount);

            string usageStr = usage.HasValue ? usage.Value.ToString() : nullString;

            string? product = mappingDictionary[arr[9]];

            try
            {
                // open values
                sbChargeable.Append("(");

                // partnerID
                sbChargeable.Append(partnerIdStr);
                sbChargeable.Append(comma);

                // product
                if (String.IsNullOrWhiteSpace(product))
                {
                    sbChargeable.Append(nullString);
                }
                else
                {
                    product = product.Trim();
                    sbChargeable.Append(singleQuote);
                    sbChargeable.Append(product);
                    sbChargeable.Append(singleQuote);
                }
                sbChargeable.Append(comma);

                // partnerPurchasedPlanID
                if (String.IsNullOrWhiteSpace(partnerPurchasedPlanID))
                {
                    sbChargeable.Append(nullString);
                }
                else
                {
                    sbChargeable.Append(singleQuote);
                    sbChargeable.Append(partnerPurchasedPlanID.Trim());
                    sbChargeable.Append(singleQuote);
                }
                sbChargeable.Append(comma);

                // plan
                string? plan = arr[7];
                if (String.IsNullOrWhiteSpace(plan))
                {
                    sbChargeable.Append(nullString);
                }
                else
                {
                    sbChargeable.Append(singleQuote);
                    sbChargeable.Append(plan.Trim());
                    sbChargeable.Append(singleQuote);
                }
                sbChargeable.Append(comma);

                // usage
                sbChargeable.Append(usageStr);

                // close values
                sbChargeable.Append("),\n");
            }
            catch (Exception e)
            {
                throw new InvalidDataException($"Error data parsing in a row {index} with message {e.Message}");
            }

            if (!String.IsNullOrWhiteSpace(product) && itemCount.HasValue)
            {
                runningCounts.Add((index, product, itemCount.Value, 0));
            }


            string domain = arr[5];
            if (String.IsNullOrWhiteSpace(domain))
            {
                throw new InvalidDataException("Domain cannot be empty");
            }
            if (!domains.ContainsKey(domain))
            {
                domains.Add(domain, partnerPurchasedPlanID);
            }
        }

        sbChargeable.Remove(sbChargeable.Length - 2, 2);
        sbChargeable.Append(";");
        string insertChargeable = sbChargeable.ToString();

        StringBuilder sbDomains = new StringBuilder("INSERT INTO domains (domain, partnerPurchasedPlanID) VALUES\n");

        foreach (var item in domains)
        {
            sbDomains.Append("('");
            sbDomains.Append(item.Key);
            sbDomains.Append(singleQuote);
            sbDomains.Append(comma);
            if (String.IsNullOrWhiteSpace(item.Value))
            {
                sbDomains.Append(nullString);
            }
            else
            {
                sbDomains.Append(singleQuote);
                sbDomains.Append(item.Value);
                sbDomains.Append(singleQuote);
            }
            sbDomains.Append("),\n");
        }
        sbDomains.Remove(sbDomains.Length - 2, 2);
        sbDomains.Append(";");
        string insertDomains = sbDomains.ToString();

        List<string> runningCountsStr = new()
        {
        "Product, ItemCount, RunningTotal, RowNo"
        };

        runningCountsStr.AddRange(runningCounts
            .OrderBy(x => x.Product)
            .ThenBy(x => x.RowNo)
            .GroupBy(s => s.Product)
            .SelectMany(g =>
            {
                int cumulative = 0;
                return g.OrderBy(s => s.RowNo)
                        .Select(s =>
                        {
                            cumulative += s.ItemCount;
                            return (s.RowNo, s.Product, s.ItemCount, cumulative);
                        });
            })
            .Select(x => $"{x.Product}, {x.ItemCount}, {x.cumulative}, {x.RowNo}")
            .ToList());

        const string s1 = "insert-chargeable.sql";
        const string s2 = "insert-domains.sql";
        const string s3 = "log-errors.txt";
        const string s4 = "log-success.txt";

        File.WriteAllText(s1, insertChargeable);
        File.WriteAllText(s2, insertDomains);
        File.WriteAllLines(s3, logMessages);
        File.WriteAllLines(s4, runningCountsStr);

        Console.WriteLine($"Insert statements generated. Please see files {s1} and {s2}.");
        Console.WriteLine($"Please find error messages in {s3}");
        Console.WriteLine($"Please find success messages in {s4}");
    }
}


