﻿// <copyright file="FunctionTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Monitor.Tests
{
    using System.Data;
    using System.Linq;
    using Npgsql;
    using Xunit;

    /// <summary>
    /// Various unit tests.
    /// </summary>
    public class FunctionTests
    {
        private const long SiemensInternetServer2017CAID = 52410;

        /// <summary>
        /// Performs an end to end test to see if the connection is basically working.
        /// </summary>
        [Fact]
        public void End2EndSmokeTest()
        {
            var connString = "Host=crt.sh;Username=guest;Database=certwatch";
            var test = new global::Monitor.Monitor();

            using (IDbConnection connection = new NpgsqlConnection(connString))
            {
                var res1 = test.SelectCertificates(connection, caID: SiemensInternetServer2017CAID, excludeRevoked: true);
                var res1b = test.SelectCertificates(connection, caID: SiemensInternetServer2017CAID, excludeRevoked: false);
                var res1bb = test.SelectCertificates(connection, caID: SiemensInternetServer2017CAID, excludeRevoked: false, onlyLINTErrors: true);

                var res2 = test.SelectCertificates(connection, caID: SiemensInternetServer2017CAID, excludeRevoked: true, daysToLookBack: 90);
                var res2b = test.SelectCertificates(connection, caID: SiemensInternetServer2017CAID, excludeRevoked: false, daysToLookBack: 90);
                var res2bb = test.SelectCertificates(connection, caID: SiemensInternetServer2017CAID, excludeRevoked: false, daysToLookBack: 90, onlyLINTErrors: true);

                var res3 = test.SelectCertificates(connection, caID: SiemensInternetServer2017CAID, excludeRevoked: true, daysToLookBack: 365);
                var res3b = test.SelectCertificates(connection, caID: SiemensInternetServer2017CAID, excludeRevoked: false, daysToLookBack: 365);
                var res3c = test.SelectCertificates(connection, caID: SiemensInternetServer2017CAID, excludeRevoked: false, excludeExpired: false, daysToLookBack: 365);
                var res3cc = test.SelectCertificates(connection, caID: SiemensInternetServer2017CAID, excludeRevoked: false, excludeExpired: false, daysToLookBack: 365, onlyLINTErrors: true);

                Assert.True(res1.Count() <= res1b.Count());
                Assert.True(res1b.Count() >= res1bb.Count());

                Assert.True(res2.Count() <= res2b.Count());
                Assert.True(res2b.Count() >= res2bb.Count());

                Assert.True(res3.Count() <= res3b.Count());
                Assert.True(res3b.Count() <= res3c.Count());
                Assert.True(res3c.Count() >= res3cc.Count());
            }
        }
    }
}
