// <copyright file="Monitor.cs" company="Siemens AG">
// Copyright (c) Siemens AG. All rights reserved.
// Licensed under the GPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Dapper;
using Newtonsoft.Json;
using Npgsql;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace Monitor
{
    /// <summary>
    /// Main class of this lambda function, exposing one method to get called from AWS lambda.
    /// </summary>
    public class Monitor
    {
        private const string SelectCertificatesByCASQLStatement = @"
select CERTIFICATE_ID,
       SERIAL_NUMBER,
       SUBJECT_DISTINGUISHED_NAME,
       CERTIFICATE_TYPE,
       NOT_BEFORE,
       NOT_AFTER,
       FIRST_SEEN,
       REVOKED,
       LINT_ERRORS,
       EXPIRED
from
    ( select C.ID CERTIFICATE_ID,
             X509_SERIALNUMBER(C.CERTIFICATE) SERIAL_NUMBER,
             X509_SUBJECTNAME(C.CERTIFICATE) SUBJECT_DISTINGUISHED_NAME,
             (CASE WHEN (x509_print(C.CERTIFICATE) LIKE '%CT Precertificate Poison%') THEN
                 'Precertificate'
             ELSE
                 'Certificate'
             END) CERTIFICATE_TYPE,
             X509_NOTBEFORE(C.CERTIFICATE) NOT_BEFORE,
             X509_NOTAFTER(C.CERTIFICATE) NOT_AFTER,
             CTLE.FIRST_SEEN FIRST_SEEN,
             COALESCE(CRL.REVOKED, 0) REVOKED,
             COALESCE(LCI.LINT_ERRORS, 0) LINT_ERRORS,
             X509_NOTAFTER(C.CERTIFICATE) < now() EXPIRED
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
     where C.ISSUER_CA_ID = @CA_ID) as ALL_CERTS
/**where**/
order by
   FIRST_SEEN desc
";

        static Monitor() => Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

        /// <summary>
        /// Selecting all certificates from a certwatch.db that matches the given criterias.
        /// </summary>
        /// <param name="connection">The connections string of the database to connect with.</param>
        /// <param name="caID">The id of the CA that shall be queried.</param>
        /// <param name="excludeRevoked">Don't select revoked certificates.</param>
        /// <param name="excludeExpired">Don't select expired certificates.</param>
        /// <param name="onlyLINTErrors">Select only certificate with linting errors.</param>
        /// <param name="excludePreCertificate">Don't select pre certificates.</param>
        /// <param name="daysToLookBack">Select only certificates new than this days.</param>
        /// <returns>A list of certificates matching the selection criteria.</returns>
        public IEnumerable<DAL.Certificate> SelectCertificates(IDbConnection connection, long caID, bool excludeRevoked = false, bool excludeExpired = true, bool onlyLINTErrors = false, bool excludePreCertificate = false, int daysToLookBack = 7)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            var builder = new SqlBuilder();
            var selector = builder.AddTemplate(SelectCertificatesByCASQLStatement);

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

            if (excludePreCertificate)
            {
                builder.Where("CERTIFICATE_TYPE <> 'Precertificate'");
            }

            builder.Where("FIRST_SEEN > now() - interval '{=DAYS_TO_LOOK_BACK} days'");

            var ca_certificates = connection.Query<DAL.Certificate>(selector.RawSql, new
            {
                CA_ID = caID,
                DAYS_TO_LOOK_BACK = daysToLookBack,
            });

            return ca_certificates;
        }

        /// <summary>
        /// Selecting all certificates that match certain criteria.
        /// </summary>
        /// <param name="apigProxyEvent">Event paramater coming from AWS API gateway.</param>
        /// <param name="context">The context under which this method is being called.</param>
        /// <returns>The list of selected certficates.</returns>
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
                return new APIGatewayProxyResponse
                {
                    Body = "No query parameters set - see documentation how to use this web services at https://github.com/RufusJWB/crt.sh-monitor",
                    StatusCode = 404,
                };
            }

            context.Logger.LogLine("apigProxyEvent.QueryStringParameters not null");

            IDictionary<string, string> caseInsensitiveHeader = new Dictionary<string, string>(apigProxyEvent.QueryStringParameters, StringComparer.OrdinalIgnoreCase);

            long caID = 0;
            string caIDString = string.Empty;
            if (caseInsensitiveHeader.TryGetValue("caID", out caIDString))
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
            if (caseInsensitiveHeader.TryGetValue("excludeExpired", out excludeExpiredString))
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

            bool excludeRevoked = true;
            string excludeRevokedString = string.Empty;
            if (caseInsensitiveHeader.TryGetValue("excludeRevoked", out excludeRevokedString))
            {
                if (!bool.TryParse(excludeRevokedString, out excludeRevoked))
                {
                    context.Logger.LogLine("excludeRevoked set but can't extracted - exiting");

                    return new APIGatewayProxyResponse
                    {
                        Body = "excludeRevoked set but can't extracted",
                        StatusCode = 404,
                    };
                }
                else
                {
                    context.Logger.LogLine($"excludeRevoked set to {excludeRevoked}");
                }
            }
            else
            {
                context.Logger.LogLine("excludeRevoked not set");
            }

            bool onlyLINTErrors = true;
            string onlyLINTErrorsString = string.Empty;
            if (caseInsensitiveHeader.TryGetValue("onlyLINTErrors", out onlyLINTErrorsString))
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
            if (caseInsensitiveHeader.TryGetValue("daysToLookBack", out daysToLookBackString))
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
            if (caseInsensitiveHeader.TryGetValue("verbose", out verboseString))
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

            bool excludePreCerticiates = false;
            string excludePreCerticiatesString = string.Empty;
            if (caseInsensitiveHeader.TryGetValue("excludePreCertificates", out excludePreCerticiatesString))
            {
                if (!bool.TryParse(excludePreCerticiatesString, out excludePreCerticiates))
                {
                    context.Logger.LogLine("excludePreCertificates set but can't extracted - exiting");

                    return new APIGatewayProxyResponse
                    {
                        Body = "excludePreCertificates set but can't extracted",
                        StatusCode = 404,
                    };
                }
                else
                {
                    context.Logger.LogLine($"excludePreCerticiates set to {excludePreCerticiates}");
                }
            }
            else
            {
                context.Logger.LogLine("excludePreCerticiates not set");
            }

            var connString = "Host=crt.sh;Username=guest;Database=certwatch;Application Name=crt.sh Monitor;Command Timeout=60";

            using (IDbConnection connection = new NpgsqlConnection(connString))
            {
                var res1 = this.SelectCertificates(connection, caID: caID, daysToLookBack: daysToLookBack, excludeExpired: excludeExpired, onlyLINTErrors: onlyLINTErrors, excludeRevoked: excludeRevoked, excludePreCertificate: excludePreCerticiates);

                if (verbose)
                {
                    var returnValue = new ReturnValues
                    {
                        CAID = caID,
                        ExcludeExpired = excludeExpired,
                        OnlyLINTErrors = onlyLINTErrors,
                        DaysToLookBack = daysToLookBack,
                        ExcludeRevoked = excludeRevoked,
                        ExcludePreCertificates = excludePreCerticiates,
                        Results = res1,
                    };

                    return new APIGatewayProxyResponse
                    {
                        Body = JsonConvert.SerializeObject(returnValue, Formatting.Indented),
                        StatusCode = 200,
                    };
                }
                else
                {
                    return new APIGatewayProxyResponse
                    {
                        Body = $"Found CAID {caID} with {res1.Count()} certificates matching the conditions: excludeExpired: {excludeExpired}, excludeRevoked: {excludeRevoked}, onlyLINTErrors: {onlyLINTErrors}, daysToLookBack: {daysToLookBack}",
                        StatusCode = 200,
                    };
                }
            }
        }
    }
}