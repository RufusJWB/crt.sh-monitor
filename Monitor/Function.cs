using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using Dapper;
using Npgsql;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace Monitor
{
    public class Function
    {
        private const string SelectCAById = @"
SELECT
    ca.ID as Id, 
    ca.NAME as Name, 
    ca.PUBLIC_KEY as PublicKey, 
    ca.BRAND as Brand, 
    ca.LINTING_APPLIES as LintingApplies, 
    ca.NO_OF_CERTS_ISSUED as NoOfCertsIssued
FROM
    ca 
WHERE
    ca.id = @CA_ID
";

        private const string SelectCA_CertificateByCAId = @"
SELECT
    ca_certificate.CA_ID as CAId, 
    ca_certificate.CERTIFICATE_ID as CertificateId
FROM
    ca_certificate 
WHERE
    ca_certificate.CA_ID = @CA_ID
";

        public IEnumerable<Tables.CA_Certificate> GetCA_Certificate(IDbConnection connection, long caID)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            var ca_certificates = connection.Query<Tables.CA_Certificate>(SelectCA_CertificateByCAId, new { CA_ID = caID });
            return ca_certificates;
        }

        public Tables.CA GetCA(IDbConnection connection, long caID)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            var ca = connection.Query<Tables.CA>(SelectCAById, new { CA_ID = caID }).Single();
            return ca;
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
