using Npgsql;
using System;
using System.Data;
using Xunit;

namespace Monitor.Tests
{
    public class FunctionTests
    {
        [Fact]
        public void End2EndSmokeTest()
        {
            var test = new Monitor.Function();

            var result = test.FunctionHandler(new[] { "test", "test" }, null);
        }

        [Fact]
        public void TestGetCA()
        {
            var connString = "Host=crt.sh;Username=guest;Database=certwatch";


            var test = new Monitor.Function();

            long SiemensInternetServer2017CAID = 52410;
            using (IDbConnection connection = new NpgsqlConnection(connString))
            {
                var result = test.GetCA(connection, SiemensInternetServer2017CAID);
            }
        }

        [Fact]
        public void TestGetCA_Certificate()
        {
            var connString = "Host=crt.sh;Username=guest;Database=certwatch";


            var test = new Monitor.Function();

            long SiemensInternetServer2017CAID = 52410;
            using (IDbConnection connection = new NpgsqlConnection(connString))
            {
                var result = test.GetCA_Certificate(connection, SiemensInternetServer2017CAID);
            }
        }
    }
}
