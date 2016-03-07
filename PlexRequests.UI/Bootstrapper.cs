﻿#region Copyright
// /************************************************************************
//    Copyright (c) 2016 Jamie Rees
//    File: Bootstrapper.cs
//    Created By: Jamie Rees
//   
//    Permission is hereby granted, free of charge, to any person obtaining
//    a copy of this software and associated documentation files (the
//    "Software"), to deal in the Software without restriction, including
//    without limitation the rights to use, copy, modify, merge, publish,
//    distribute, sublicense, and/or sell copies of the Software, and to
//    permit persons to whom the Software is furnished to do so, subject to
//    the following conditions:
//   
//    The above copyright notice and this permission notice shall be
//    included in all copies or substantial portions of the Software.
//   
//    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
//    EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
//    MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
//    NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
//    LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
//    OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
//    WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//  ************************************************************************/
#endregion

using FluentScheduler;
using Mono.Data.Sqlite;

using Nancy;
using Nancy.Authentication.Forms;
using Nancy.Bootstrapper;
using Nancy.Cryptography;
using Nancy.Diagnostics;
using Nancy.Session;
using Nancy.TinyIoc;

using PlexRequests.Core;
using PlexRequests.Core.SettingModels;
using PlexRequests.Helpers;
using PlexRequests.Services;
using PlexRequests.Services.Interfaces;
using PlexRequests.Store;
using PlexRequests.Store.Repository;
using PlexRequests.UI.Jobs;

using IAvailabilityChecker = PlexRequests.UI.Jobs.IAvailabilityChecker;
using PlexAvailabilityChecker = PlexRequests.UI.Jobs.PlexAvailabilityChecker;
using TaskFactory = FluentScheduler.TaskFactory;

namespace PlexRequests.UI
{
    public class Bootstrapper : DefaultNancyBootstrapper
    {
        // The bootstrapper enables you to reconfigure the composition of the framework,
        // by overriding the various methods and properties.
        // For more information https://github.com/NancyFx/Nancy/wiki/Bootstrapper

        protected override void ConfigureRequestContainer(TinyIoCContainer container, NancyContext context)
        {
            container.Register<IUserMapper, UserMapper>();

            container.Register<ISqliteConfiguration, DbConfiguration>(new DbConfiguration(new SqliteFactory()));
            
            container.Register<ISettingsRepository, JsonRepository>();
            container.Register<ICacheProvider, MemoryCacheProvider>();

            container.Register<ISettingsService<PlexRequestSettings>, SettingsServiceV2<PlexRequestSettings>>();
            container.Register<ISettingsService<CouchPotatoSettings>, SettingsServiceV2<CouchPotatoSettings>>();
            container.Register<ISettingsService<AuthenticationSettings>, SettingsServiceV2<AuthenticationSettings>>();
            container.Register<ISettingsService<PlexSettings>, SettingsServiceV2<PlexSettings>>();
            container.Register<IRepository<RequestedModel>, GenericRepository<RequestedModel>>();
            container.Register<IAvailabilityChecker, PlexAvailabilityChecker>();
            container.Register<IRequestService, RequestService>();

            container.Register<IAvailabilityChecker, PlexAvailabilityChecker>();
            container.Register<IConfigurationReader, ConfigurationReader>();
            container.Register<IIntervals, UpdateInterval>();

            base.ConfigureRequestContainer(container, context);
        }

        protected override void ApplicationStartup(TinyIoCContainer container, IPipelines pipelines)
        {
            TaskManager.TaskFactory = new Jobs.PlexTaskFactory();
            TaskManager.Initialize(new PlexRegistry());

            CookieBasedSessions.Enable(pipelines, CryptographyConfiguration.Default);
            
            StaticConfiguration.DisableErrorTraces = false;

            base.ApplicationStartup(container, pipelines);

            // Enable forms auth
            var formsAuthConfiguration = new FormsAuthenticationConfiguration
            {
                RedirectUrl = "~/login",
                UserMapper = container.Resolve<IUserMapper>()
            };

            FormsAuthentication.Enable(pipelines, formsAuthConfiguration);
        }

        protected override DiagnosticsConfiguration DiagnosticsConfiguration => new DiagnosticsConfiguration { Password = @"password" };
    }
}