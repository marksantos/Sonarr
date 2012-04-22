﻿using System;
using System.Collections.Generic;
using System.Linq;
using Exceptrack.Driver;
using NLog;
using NzbDrone.Common.Contract;

namespace NzbDrone.Common
{
    public static class ReportingService
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private const string SERVICE_URL = "http://services.nzbdrone.com/reporting";
        private const string PARSE_URL = SERVICE_URL + "/ParseError";
        private const string EXCEPTION_URL = SERVICE_URL + "/ReportException";

        public static RestProvider RestProvider { get; set; }
        public static ExceptionClient ExceptrackDriver { get; set; }


        private static readonly HashSet<string> parserErrorCache = new HashSet<string>();

        public static void ClearCache()
        {
            lock (parserErrorCache)
            {
                parserErrorCache.Clear();
            }
        }

        public static void ReportParseError(string title)
        {
            try
            {
                VerifyDependencies();

                lock (parserErrorCache)
                {
                    if (parserErrorCache.Contains(title.ToLower())) return;

                    parserErrorCache.Add(title.ToLower());
                }

                var report = new ParseErrorReport { Title = title };
                RestProvider.PostData(PARSE_URL, report);
            }
            catch (Exception e)
            {
                if (!EnvironmentProvider.IsProduction)
                {
                    throw;
                }

                e.Data.Add("title", title);
                logger.InfoException("Unable to report parse error", e);
            }
        }

        public static void ReportException(LogEventInfo logEvent)
        {
            try
            {
                VerifyDependencies();

                var exceptionData = new ExceptionData();

                exceptionData.Exception = logEvent.Exception;
                exceptionData.Location = logEvent.LoggerName;
                exceptionData.UserId = EnvironmentProvider.UGuid.ToString().Replace("-", string.Empty);

                ExceptrackDriver.SubmitException(exceptionData);
            }
            catch (Exception e)
            {
                if (!EnvironmentProvider.IsProduction)
                {
                    throw;
                }

                //this shouldn't log an exception since it will cause a recursive loop.
                logger.Info("Unable to report exception. " + e);
            }
        }


        public static void SetupExceptrackDriver()
        {
            ExceptrackDriver = new ExceptionClient(
                                                   "CB230C312E5C4FF38B4FB9644B05E60D",
                                                   new EnvironmentProvider().Version.ToString(),
                                                   new Uri("http://api.exceptrack.com/"));
        }

        private static void VerifyDependencies()
        {
            if (RestProvider == null)
            {
                if (EnvironmentProvider.IsProduction)
                {
                    logger.Warn("Rest provider wasn't provided. creating new one!");
                    RestProvider = new RestProvider(new EnvironmentProvider());
                }
                else
                {
                    throw new InvalidOperationException("REST Provider wasn't configured correctly.");
                }
            }

            if (ExceptrackDriver == null)
            {
                if (EnvironmentProvider.IsProduction)
                {
                    logger.Warn("Exceptrack Driver wasn't provided. creating new one!");
                    SetupExceptrackDriver();
                }
                else
                {
                    throw new InvalidOperationException("Exceptrack Driver wasn't configured correctly.");
                }
            }
        }
    }
}
