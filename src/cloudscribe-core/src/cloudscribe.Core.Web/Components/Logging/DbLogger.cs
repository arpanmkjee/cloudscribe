﻿// Copyright (c) Source Tree Solutions, LLC. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
//	Author:                 Joe Audette
//  Created:			    2011-08-19
//	Last Modified:		    2015-08-19
// 

using cloudscribe.Core.Models.Logging;
using Microsoft.AspNet.Hosting;
using Microsoft.AspNet.Http.Features;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.Logging;
using System;
using System.Collections;
using System.Globalization;
using System.Text;

namespace cloudscribe.Core.Web.Components.Logging
{
    public class DbLogger : ILogger
    {
        public DbLogger(
            string loggerName,
            LogLevel minimumLevel,
            IServiceProvider serviceProvider,
            ILogRepository logRepository)
        {
            logger = loggerName;
            logRepo = logRepository;
            services = serviceProvider;
            this.minimumLevel = minimumLevel;
        }

        private ILogRepository logRepo;
        private IHttpContextAccessor contextAccessor = null;
        private IServiceProvider services;
        private const int _indentation = 2;
        private string logger = string.Empty;
        private LogLevel minimumLevel;

        #region ILogger
        public void Log(
            LogLevel logLevel, 
            int eventId, 
            object state, 
            Exception exception, 
            Func<object, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }
            var message = string.Empty;
            var values = state as ILogValues;
            if (formatter != null)
            {
                message = formatter(state, exception);
            }
            else if (values != null)
            {
                var builder = new StringBuilder();
                FormatLogValues(
                    builder,
                    values,
                    level: 1,
                    bullet: false);

                message = builder.ToString();
                if (exception != null)
                {
                    message += Environment.NewLine + exception;
                }
            }
            else
            {
                message = LogFormatter.Formatter(state, exception);
            }
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            contextAccessor = services.GetService<IHttpContextAccessor>();

            string ipAddress = GetIpAddress();
            string culture = CultureInfo.CurrentCulture.Name;
            string url = GetRequestUrl();
            string shortUrl = GetShortUrl(url);
            string thread = System.Threading.Thread.CurrentThread.Name + " " + eventId.ToString();
            string logLev = logLevel.ToString();
            
            logRepo.AddLogItem(
                DateTime.UtcNow,
                ipAddress,
                culture,
                url,
                shortUrl,
                thread,
                logLev,
                logger,
                message);

        }

        public bool IsEnabled(LogLevel logLevel)
        {
            //Debug = 1,
            //Verbose = 2,
            //Information = 3,
            //Warning = 4,
            //Error = 5,
            //Critical = 6,
            
            return (logLevel >= minimumLevel);
        }

        public IDisposable BeginScopeImpl(object state)
        {
            return new NoopDisposable();
        }

        #endregion

        private string GetIpAddress()
        {
            if((contextAccessor != null)&&(contextAccessor.HttpContext != null))
            {
                var connection = contextAccessor.HttpContext.GetFeature<IHttpConnectionFeature>();
                if(connection != null)
                {
                    return connection.RemoteIpAddress.ToString();
                }
                
            }

            return string.Empty;

        }

        private string GetShortUrl(string url)
        {
            if(url.Length > 255)
            {
                return url.Substring(0, 254);
            }

            return url;
        }

        private string GetRequestUrl()
        {
            if ((contextAccessor != null) && (contextAccessor.HttpContext != null))
            {
                return contextAccessor.HttpContext.Request.Path.ToString();
            }

            return string.Empty;
        }

        private void FormatLogValues(StringBuilder builder, ILogValues logValues, int level, bool bullet)
        {
            var values = logValues.GetValues();
            if (values == null)
            {
                return;
            }
            var isFirst = true;
            foreach (var kvp in values)
            {
                builder.AppendLine();
                if (bullet && isFirst)
                {
                    builder.Append(' ', level * _indentation - 1)
                           .Append('-');
                }
                else
                {
                    builder.Append(' ', level * _indentation);
                }
                builder.Append(kvp.Key)
                       .Append(": ");

                if (kvp.Value is IEnumerable && !(kvp.Value is string))
                {
                    foreach (var value in (IEnumerable)kvp.Value)
                    {
                        if (value is ILogValues)
                        {
                            FormatLogValues(
                                builder,
                                (ILogValues)value,
                                level + 1,
                                bullet: true);
                        }
                        else
                        {
                            builder.AppendLine()
                                   .Append(' ', (level + 1) * _indentation)
                                   .Append(value);
                        }
                    }
                }
                else if (kvp.Value is ILogValues)
                {
                    FormatLogValues(
                        builder,
                        (ILogValues)kvp.Value,
                        level + 1,
                        bullet: false);
                }
                else
                {
                    builder.Append(kvp.Value);
                }
                isFirst = false;
            }
        }

        private class NoopDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }

    }
}
