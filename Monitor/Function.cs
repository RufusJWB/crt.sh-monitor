using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using Npgsql;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace Monitor
{
    public class Function
    {
        public int GetNumberOfLINTError(string ca)
        {
            var connString = "Host=crt.sh;Username=guest;Database=certwatch";

            using (var conn = new NpgsqlConnection(connString))
            {
                conn.Open();

                ////// Insert some data
                ////using (var cmd = new NpgsqlCommand())
                ////{
                ////    cmd.Connection = conn;
                ////    cmd.CommandText = "INSERT INTO data (some_field) VALUES (@p)";
                ////    cmd.Parameters.AddWithValue("p", "Hello world");
                ////    cmd.ExecuteNonQuery();
                ////}

                ////// Retrieve all rows
                ////using (var cmd = new NpgsqlCommand("SELECT some_field FROM data", conn))
                ////using (var reader = cmd.ExecuteReader())
                ////    while (reader.Read())
                ////        Console.WriteLine(reader.GetString(0));
            }
            return 1;
        }

        /// <summary>
        /// A simple function that takes a string and does a ToUpper
        /// </summary>
        /// <param name="input"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public string FunctionHandler(string[] fqdns, ILambdaContext context)
        {
            string result = string.Join(", ", fqdns);
            return result;
        }
    }
}
