using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Dapper;
using Newtonsoft.Json.Linq;
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
select CERTIFICATE_ID,
       SERIAL_NUMBER,
       SUBJECT_DISTINGUISHED_NAME,
       NOT_BEFORE,
       NOT_AFTER,
       FIRST_SEEN,
       REVOKED,
       LINT_ERRORS
from
    ( select C.ID CERTIFICATE_ID,
             X509_SERIALNUMBER(C.CERTIFICATE) SERIAL_NUMBER,
             X509_SUBJECTNAME(C.CERTIFICATE) SUBJECT_DISTINGUISHED_NAME,
             X509_NOTBEFORE(C.CERTIFICATE) NOT_BEFORE,
             X509_NOTAFTER(C.CERTIFICATE) NOT_AFTER,
             CTLE.FIRST_SEEN FIRST_SEEN,
             COALESCE(CRL.REVOKED, 0) REVOKED,
             COALESCE(LCI.LINT_ERRORS, 0) LINT_ERRORS
     from CERTIFICATE C
     join lateral
         (select MIN(CTLE.ENTRY_TIMESTAMP) FIRST_SEEN,
                 CTLE.CERTIFICATE_ID
          from CT_LOG_ENTRY CTLE
          where CTLE.CERTIFICATE_ID = C.ID
          group by CTLE.CERTIFICATE_ID) CTLE on true
     left join lateral
         (select COUNT(CRL.CA_ID) REVOKED,
                 CRL.SERIAL_NUMBER
          from CRL_REVOKED CRL
          where CRL.CA_ID = C.ISSUER_CA_ID
              and CRL.SERIAL_NUMBER = X509_SERIALNUMBER(C.CERTIFICATE)
          group by CRL.SERIAL_NUMBER) CRL on true
     left join lateral
         (select COUNT(LCI.CERTIFICATE_ID) LINT_ERRORS,
                 LCI.CERTIFICATE_ID
          from LINT_CERT_ISSUE LCI
          where LCI.CERTIFICATE_ID = C.ID
          group by LCI.CERTIFICATE_ID) LCI on true
     where C.ISSUER_CA_ID = 52410) as ALL_CERTS
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

            if (onlyLINTErrors)
            {
                builder.Where("LINT_ERRORS > 0");
            }

            if (excludeExpired)
            {
                builder.Where("NOT_AFTER > now()");
            }

            builder.Where("FIRST_SEEN > now() - interval '{=DAYS_TO_LOOK_BACK} days'");

            var ca_certificates = connection.Query<DAL.Certificate>(selector.RawSql, new
            {
                CA_ID = caID,
                DAYS_TO_LOOK_BACK = daysToLookBack
            });

            return ca_certificates;
        }

        public APIGatewayProxyResponse FunctionHandler(APIGatewayProxyRequest apigProxyEvent, ILambdaContext context)
        {
            context.Logger.LogLine("FunctionHandler started");

            if (apigProxyEvent == null)
            {
                throw new ArgumentNullException(nameof(apigProxyEvent));
            }

            context.Logger.LogLine("apigProxyEvent not null");

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            context.Logger.LogLine("context not null");

            if (apigProxyEvent.QueryStringParameters == null)
            {
                throw new ArgumentNullException(nameof(apigProxyEvent.QueryStringParameters));
            }

            context.Logger.LogLine("apigProxyEvent.QueryStringParameters not null");

            if (apigProxyEvent.QueryStringParameters.Keys == null)
            {
                throw new ArgumentNullException(nameof(apigProxyEvent.QueryStringParameters.Keys));
            }

            context.Logger.LogLine("apigProxyEvent.QueryStringParameters.Keys not null");

            if (!apigProxyEvent.QueryStringParameters.Keys.Contains("caid"))
            {
                return new APIGatewayProxyResponse
                {
                    Body = "No CAID found",
                    StatusCode = 406,
                };
            }

            context.Logger.LogLine($"Hallo CA: {apigProxyEvent.QueryStringParameters["caid"]}");

            long caID = 0;
            string caIDString = string.Empty;
            if (apigProxyEvent.QueryStringParameters.TryGetValue("caID", out caIDString))
            {
                if (!long.TryParse(caIDString, out caID))
                {
                    context.Logger.LogLine("caID set but can't extracted - exiting");

                    return new APIGatewayProxyResponse
                    {
                        Body = "caID set but can't extracted",
                        StatusCode = 404,
                    };
                }
                else
                {
                    context.Logger.LogLine($"caID set to {caID}");
                }
            }
            else
            {
                context.Logger.LogLine("caID not set - exiting");

                return new APIGatewayProxyResponse
                {
                    Body = "caID not set",
                    StatusCode = 404,
                };
            }

            if (caID <= 0)
            {
                context.Logger.LogLine("caID not set - exiting");

                return new APIGatewayProxyResponse
                {
                    Body = "caID not set",
                    StatusCode = 404,
                };
            }

            bool excludeExpired = true;
            string excludeExpiredString = string.Empty;
            if (apigProxyEvent.QueryStringParameters.TryGetValue("excludeExpired", out excludeExpiredString))
            {
                if (!bool.TryParse(excludeExpiredString, out excludeExpired))
                {
                    context.Logger.LogLine("excludeExpired set but can't extracted - exiting");

                    return new APIGatewayProxyResponse
                    {
                        Body = "excludeExpired set but can't extracted",
                        StatusCode = 404,
                    };
                }
                else
                {
                    context.Logger.LogLine($"excludeExpired set to {excludeExpired}");
                }
            }
            else
            {
                context.Logger.LogLine("excludeExpired not set");
            }

            bool onlyLINTErrors = true;
            string onlyLINTErrorsString = string.Empty;
            if (apigProxyEvent.QueryStringParameters.TryGetValue("onlyLINTErrors", out onlyLINTErrorsString))
            {
                if (!bool.TryParse(onlyLINTErrorsString, out onlyLINTErrors))
                {
                    context.Logger.LogLine("onlyLINTErrors set but can't extracted - exiting");

                    return new APIGatewayProxyResponse
                    {
                        Body = "onlyLINTErrors set but can't extracted",
                        StatusCode = 404,
                    };
                }
                else
                {
                    context.Logger.LogLine($"onlyLINTErrors set to {excludeExpired}");
                }
            }
            else
            {
                context.Logger.LogLine("onlyLINTErrors not set");
            }

            int daysToLookBack = 3650;
            string daysToLookBackString = string.Empty;
            if (apigProxyEvent.QueryStringParameters.TryGetValue("daysToLookBack", out daysToLookBackString))
            {
                if (!int.TryParse(daysToLookBackString, out daysToLookBack))
                {
                    context.Logger.LogLine("daysToLookBack set but can't extracted - exiting");

                    return new APIGatewayProxyResponse
                    {
                        Body = "daysToLookBack set but can't extracted",
                        StatusCode = 404,
                    };
                }
                else
                {
                    context.Logger.LogLine($"daysToLookBack set to {daysToLookBack}");
                }
            }
            else
            {
                context.Logger.LogLine("daysToLookBack not set");
            }

            bool verbose = true;
            string verboseString = string.Empty;
            if (apigProxyEvent.QueryStringParameters.TryGetValue("verbose", out verboseString))
            {
                if (!bool.TryParse(verboseString, out verbose))
                {
                    context.Logger.LogLine("verbose set but can't extracted - exiting");

                    return new APIGatewayProxyResponse
                    {
                        Body = "verbose set but can't extracted",
                        StatusCode = 404,
                    };
                }
                else
                {
                    context.Logger.LogLine($"verbose set to {verbose}");
                }
            }
            else
            {
                context.Logger.LogLine("verbose not set");
            }

            var connString = "Host=crt.sh;Username=guest;Database=certwatch";

            using (IDbConnection connection = new NpgsqlConnection(connString))
            {
                var res1 = SelectCertificates(connection, caID: caID, daysToLookBack: daysToLookBack, excludeExpired: excludeExpired, onlyLINTErrors: onlyLINTErrors);

                if (verbose)
                {
                    dynamic result = new JObject();
                    result.CAID = caID;
                    result.Parameter = new JObject();
                    result.Paramater.excludeExpired = excludeExpired;
                    result.Paramater.onlyLINTErrors = onlyLINTErrors;
                    result.Paramater.daysToLookBack = daysToLookBack;
                    result.Results = new JArray(res1.ToArray());

                    return new APIGatewayProxyResponse
                    {
                        Body = result.toString(),
                        StatusCode = 200,
                    };
                }
                else
                {
                return new APIGatewayProxyResponse
                {
                    Body = $"Found CAID {caID} with {res1.Count()} certificates matching the conditions: excludeExpired: {excludeExpired}, onlyLINTErrors: {onlyLINTErrors}, daysToLookBack: {daysToLookBack}",
                    StatusCode = 200,
                };
                }

            }
        }
    }
}