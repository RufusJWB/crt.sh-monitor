using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Dapper;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace Monitor
{
    public class Function
    {
        static Function()
        {
            Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
        }

        private const string SelectCertificatesByCA = @"
select
   CERTIFICATE_ID,
   SERIAL_NUMBER,
   SUBJECT_DISTINGUISHED_NAME,
   NOT_BEFORE,
   NOT_AFTER,
   FIRST_SEEN,
   REVOKED,
   LINT_ERRORS 
from
   (
      select
         C.ID CERTIFICATE_ID,
         X509_SERIALNUMBER(C.CERTIFICATE) SERIAL_NUMBER,
         X509_SUBJECTNAME(C.CERTIFICATE) SUBJECT_DISTINGUISHED_NAME,
         X509_NOTBEFORE(C.CERTIFICATE) NOT_BEFORE,
         X509_NOTAFTER(C.CERTIFICATE) NOT_AFTER,
         (
            select
               min(CLE.ENTRY_TIMESTAMP) 
            from
               CT_LOG_ENTRY CLE 
            where
               CLE.CERTIFICATE_ID = C.ID 
         )
         FIRST_SEEN,
         (
            select
               count(CRL.CA_ID) 
            from
               CRL_REVOKED CRL 
            where
               CRL.CA_ID = 52410 
               and CRL.SERIAL_NUMBER = X509_SERIALNUMBER(C.CERTIFICATE) 
         )
         REVOKED,
         (
            select
               count(LCI.CERTIFICATE_ID) 
            from
               LINT_CERT_ISSUE LCI 
            where
               LCI.CERTIFICATE_ID = C.ID 
         )
         LINT_ERRORS 
      from
         CERTIFICATE C 
      where
         C.ISSUER_CA_ID = @CA_ID 
   )
   as ALL_CERTS 
/**where**/
order by
   FIRST_SEEN desc
";

        public IEnumerable<DAL.Certificate> SelectCertificates(IDbConnection connection, long caID, bool excludeRevoked = false, bool excludeExpired = true, bool onlyLINTErrors = false, int daysToLookBack = 7)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            var builder = new SqlBuilder();
            var selector = builder.AddTemplate(SelectCertificatesByCA);

            if (excludeRevoked)
            {
                builder.Where("REVOKED = 0");
            }

            if (excludeExpired)
            {
                builder.Where("NOT_AFTER > now()");
            }

            if (onlyLINTErrors)
            {
                builder.Where("LINT_ERRORS > 0");
            }

            builder.Where("now() - interval '{=DAYS_TO_LOOK_BACK} days' < FIRST_SEEN");

            var ca_certificates = connection.Query<DAL.Certificate>(selector.RawSql, new
            {
                CA_ID = caID,
                DAYS_TO_LOOK_BACK = daysToLookBack
            });

            return ca_certificates;
        }

        public APIGatewayProxyResponse FunctionHandler(APIGatewayProxyRequest apigProxyEvent, ILambdaContext context)
        {
            context.Logger.LogLine($"Hallo CA: {apigProxyEvent.QueryStringParameters["caid"]}");
            int caID = 0;

            if (Int32.TryParse(apigProxyEvent.QueryStringParameters["caid"], out caID))
            {
                var connString = "Host=crt.sh;Username=guest;Database=certwatch";

                using (IDbConnection connection = new NpgsqlConnection(connString))
                {
                    var res1 = SelectCertificates(connection, caID: caID, daysToLookBack: 3650, excludeExpired:true, onlyLINTErrors:true);

                    return new APIGatewayProxyResponse
                    {
                        Body = $"Found CAID {caID} with {res1.Count()} certificates",
                        StatusCode = 200,
                    };
                }
            }
            else
            {
                return new APIGatewayProxyResponse
                {
                    Body = "No CAID found",
                    StatusCode = 404,
                };
            }
        }
    }
}
