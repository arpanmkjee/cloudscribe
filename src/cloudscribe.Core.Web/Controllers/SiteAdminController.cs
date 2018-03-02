﻿// Copyright (c) Source Tree Solutions, LLC. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
// Author:					Joe Audette
// Created:					2014-10-26
// Last Modified:			2018-03-02
// 

using cloudscribe.Core.Models;
using cloudscribe.Core.Web.Components;
using cloudscribe.Core.Web.ViewModels.SiteSettings;
using cloudscribe.Web.Common;
using cloudscribe.Web.Common.Extensions;
using cloudscribe.Web.Common.Razor;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace cloudscribe.Core.Web.Controllers.Mvc
{
    [Authorize(Policy = "AdminPolicy")]
    public class SiteAdminController : Controller
    {
        public SiteAdminController(
            SiteManager siteManager,
            GeoDataManager geoDataManager,
            ISiteAcountCapabilitiesProvider siteCapabilities,
            IOptions<MultiTenantOptions> multiTenantOptions,
            IOptions<UIOptions> uiOptionsAccessor,
            IThemeListBuilder layoutListBuilder,
            IStringLocalizer<CloudscribeCore> localizer,
            ITimeZoneHelper timeZoneHelper,
            IOptions<RequestLocalizationOptions> localizationOptions
            )
        {
            if (geoDataManager == null) { throw new ArgumentNullException(nameof(geoDataManager)); }
            if (multiTenantOptions == null) { throw new ArgumentNullException(nameof(multiTenantOptions)); }

            _multiTenantOptions = multiTenantOptions.Value;
            _siteManager = siteManager ?? throw new ArgumentNullException(nameof(siteManager));
            _geoDataManager = geoDataManager;
            _uiOptions = uiOptionsAccessor.Value;
            _layoutListBuilder = layoutListBuilder;
            _sr = localizer;
            _tzHelper = timeZoneHelper;
            _siteCapabilities = siteCapabilities;
            _localization = localizationOptions.Value;
        }

        private SiteManager _siteManager;
        private GeoDataManager _geoDataManager;
        private MultiTenantOptions _multiTenantOptions;
        private ISiteAcountCapabilitiesProvider _siteCapabilities;
        
        private IStringLocalizer _sr;
        private IThemeListBuilder _layoutListBuilder;
        private UIOptions _uiOptions;
        private ITimeZoneHelper _tzHelper;
        private RequestLocalizationOptions _localization;

        // GET: /SiteAdmin
        [HttpGet]
        public IActionResult Index()
        {
            ViewData["Title"] = _sr["Site Administration"];
            // this view just has navigation map
            return View();
        }

        // GET: /SiteAdmin
        [HttpGet]
        public IActionResult Security()
        {
            ViewData["Title"] = _sr["Security Settings"];
            // this view just has navigation map
            return View("Index");
        }


        [HttpGet]
        [Authorize(Policy = "ServerAdminPolicy")]
        public async Task<IActionResult> SiteList(int pageNumber = 1, int pageSize = -1)
        {
            ViewData["Title"] = _sr["Site List"];

            int itemsPerPage = _uiOptions.DefaultPageSize_SiteList;
            if (pageSize > 0)
            {
                itemsPerPage = pageSize;
            }
            
            var filteredSiteId = Guid.Empty; //nothing filtered
            var sites = await _siteManager.GetPageOtherSites(
                filteredSiteId,
                pageNumber,
                itemsPerPage);
            
            var model = new SiteListViewModel();
            model.Sites = sites;
            
            return View(model);

        }

        // GET: /SiteAdmin/SiteInfo
        [HttpGet]
        [Authorize(Policy = "AdminPolicy")]
        public async Task<IActionResult> SiteInfo(
            Guid? siteId,
            int slp = 1)
        {

            ISiteSettings selectedSite;
            if(siteId.HasValue)
            {
                selectedSite = await _siteManager.Fetch(siteId.Value);
            }
            else
            {
                selectedSite = await _siteManager.Fetch(_siteManager.CurrentSite.Id);
            }
            // only server admin site can edit other sites settings
            if (selectedSite.Id != _siteManager.CurrentSite.Id)
            {
                ViewData["Title"] = string.Format(CultureInfo.CurrentUICulture, _sr["{0} - Settings"], selectedSite.SiteName);
            }
            else
            {
                ViewData["Title"] = _sr["Site Settings"];
            }
            
            var model = new SiteBasicSettingsViewModel();
            model.ReturnPageNumber = slp; // site list page number to return to
            model.TimeZoneId = selectedSite.TimeZoneId;
           

            model.SiteId = selectedSite.Id;
            model.SiteName = selectedSite.SiteName;
            model.AliasId = selectedSite.AliasId;
            model.GoogleAnalyticsProfileId = selectedSite.GoogleAnalyticsProfileId;
            model.AddThisProfileId = selectedSite.AddThisDotComUsername;
            
            model.IsClosed = selectedSite.SiteIsClosed;
            model.ClosedMessage = selectedSite.SiteIsClosedMessage;
            
            if (_multiTenantOptions.Mode == MultiTenantMode.FolderName)
            {
                model.SiteFolderName = selectedSite.SiteFolderName;
            }
            else if (_multiTenantOptions.Mode == MultiTenantMode.HostName)
            {
                model.HostName = selectedSite.PreferredHostName;
            }
            
            model.Theme = selectedSite.Theme;
            
            
            // can only delete from server admin site/cannot delete server admin site
            if (_siteManager.CurrentSite.IsServerAdminSite)
            {
                if (model.SiteId != _siteManager.CurrentSite.Id)
                {
                    model.ShowDelete = _uiOptions.AllowDeleteChildSites;
                }
            }

            PopulateLists(model, selectedSite);
            
            return View(model);
        }

        private void PopulateLists(SiteBasicSettingsViewModel model, ISiteSettings selectedSite)
        {
            model.AvailableThemes = _layoutListBuilder.GetAvailableThemes(selectedSite.AliasId);
            model.AllTimeZones = _tzHelper.GetTimeZoneList().Select(x =>
                              new SelectListItem
                              {
                                  Text = x,
                                  Value = x,
                                  Selected = model.TimeZoneId == x
                              });

            model.ForcedUICulture = selectedSite.ForcedUICulture;
            model.AvailableUICultures = _localization.SupportedUICultures
                                      .Select(c => new SelectListItem { Value = c.Name, Text = c.Name, Selected = model.ForcedUICulture == c.Name })
                                      .ToList();
            model.AvailableUICultures.Insert(0, new SelectListItem { Value = "", Text = _sr["Any"] });

            model.ForcedCulture = selectedSite.ForcedCulture;
            model.AvailableCultures = _localization.SupportedCultures
                                      .Select(c => new SelectListItem { Value = c.Name, Text = c.Name, Selected = model.ForcedCulture == c.Name })
                                      .ToList();
            model.AvailableCultures.Insert(0, new SelectListItem { Value = "", Text = _sr["Any"] });
        }

        // Post: /SiteAdmin/SiteInfo
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminPolicy")]
        public async Task<ActionResult> SiteInfo(SiteBasicSettingsViewModel model)
        {
            // can only delete from server admin site/cannot delete server admin site
            if (_siteManager.CurrentSite.IsServerAdminSite)
            {
                if (model.SiteId != _siteManager.CurrentSite.Id)
                {
                    model.ShowDelete = _uiOptions.AllowDeleteChildSites;
                }
            }
            
            if (model.SiteId == Guid.Empty)
            {
                this.AlertDanger(_sr["oops something went wrong, site was not found."], true);

                return RedirectToAction("Index");
            }

            ISiteSettings selectedSite = null;
            if (model.SiteId == _siteManager.CurrentSite.Id)
            {
                selectedSite = await _siteManager.GetCurrentSiteSettings();
                ViewData["Title"] = _sr["Site Settings"];
            }
            else if (_siteManager.CurrentSite.IsServerAdminSite)
            {
                selectedSite = await _siteManager.Fetch(model.SiteId);
                ViewData["Title"] = string.Format(CultureInfo.CurrentUICulture, _sr["{0} - Settings"], selectedSite.SiteName);
            }

            if (!ModelState.IsValid)
            {
                PopulateLists(model, selectedSite);
                return View(model);
            }

            if (selectedSite == null)
            {
                this.AlertDanger(_sr["oops something went wrong."], true);

                return RedirectToAction("Index");
            }

            if (_multiTenantOptions.Mode == MultiTenantMode.FolderName)
            {
                if (
                    ((model.SiteFolderName == null) || (model.SiteFolderName.Length == 0))
                    && (!selectedSite.IsServerAdminSite)
                    )
                {
                    // only the server admin site can be without a folder
                    ModelState.AddModelError("foldererror", _sr["Folder name is required."]);
                    PopulateLists(model, selectedSite);
                    return View(model);
                }
                
                var folderAvailable = await _siteManager.FolderNameIsAvailable(selectedSite.Id, model.SiteFolderName);
                if (!folderAvailable)
                {
                    ModelState.AddModelError("foldererror", "The selected folder name is already in use on another site.");
                    PopulateLists(model, selectedSite);
                    return View(model);
                }

            }
            else if (_multiTenantOptions.Mode == MultiTenantMode.HostName)
            {
                ISiteHost host;
                if (!string.IsNullOrEmpty(model.HostName))
                {
                    model.HostName = model.HostName.Replace("https://", string.Empty).Replace("http://", string.Empty);

                    host = await _siteManager.GetSiteHost(model.HostName);

                    if (host != null)
                    {
                        if (host.SiteId != selectedSite.Id)
                        {
                            ModelState.AddModelError("hosterror", _sr["The selected host/domain name is already in use on another site."]);
                            PopulateLists(model, selectedSite);
                            return View(model);
                        }
                    }
                    else
                    {
                        await _siteManager.AddHost(
                            selectedSite.Id,
                            model.HostName);
                    }
                }
                
                selectedSite.PreferredHostName = model.HostName;
            }
            
            selectedSite.SiteName = model.SiteName;
            selectedSite.TimeZoneId = model.TimeZoneId;
            selectedSite.SiteFolderName = model.SiteFolderName;
            selectedSite.SiteIsClosed = model.IsClosed;
            selectedSite.SiteIsClosedMessage = model.ClosedMessage;
            selectedSite.Theme = model.Theme;
            selectedSite.GoogleAnalyticsProfileId = model.GoogleAnalyticsProfileId;
            selectedSite.AddThisDotComUsername = model.AddThisProfileId;

            selectedSite.ForcedCulture = model.ForcedCulture;
            selectedSite.ForcedUICulture = model.ForcedUICulture;
            
            await _siteManager.Update(selectedSite);
            
            this.AlertSuccess(string.Format(_sr["Basic site settings for {0} were successfully updated."],
                        selectedSite.SiteName), true);
            
            return RedirectToAction("Index");
        }

        // GET: /SiteAdmin/NewSite
        [HttpGet]
        [Authorize(Policy = "ServerAdminPolicy")]
        public ActionResult NewSite(int slp = 1)
        {
            ViewData["Title"] = _sr["Create New Site"];

            var model = new NewSiteViewModel();
            model.ReturnPageNumber = slp; //site list return page
            model.SiteId = Guid.Empty;
            model.TimeZoneId = _siteManager.CurrentSite.TimeZoneId;
            model.AllTimeZones = _tzHelper.GetTimeZoneList().Select(x =>
                               new SelectListItem
                               {
                                   Text = x,
                                   Value = x,
                                   Selected = model.TimeZoneId == x
                               });
            
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "ServerAdminPolicy")]
        public async Task<ActionResult> NewSite(NewSiteViewModel model)
        {
            ViewData["Title"] = "Create New Site";

            if (!ModelState.IsValid)
            {
                return View(model);
            }
            
            bool addHostName = false;
            var newSite = new SiteSettings();
            newSite.Id = Guid.NewGuid();
            
            if (_multiTenantOptions.Mode == MultiTenantMode.FolderName)
            {
                if (string.IsNullOrEmpty(model.SiteFolderName))
                {
                    model.AllTimeZones = _tzHelper.GetTimeZoneList().Select(x =>
                               new SelectListItem
                               {
                                   Text = x,
                                   Value = x,
                                   Selected = model.TimeZoneId == x
                               });
                    ModelState.AddModelError("foldererror", _sr["Folder name is required."]);
                    return View(model);
                }
                else
                {
                    //https://github.com/joeaudette/cloudscribe.StarterKits/issues/27
                    model.SiteFolderName = model.SiteFolderName.ToLowerInvariant();
                }

                bool folderAvailable = await _siteManager.FolderNameIsAvailable(newSite.Id, model.SiteFolderName);
                if (!folderAvailable)
                {
                    model.AllTimeZones = _tzHelper.GetTimeZoneList().Select(x =>
                               new SelectListItem
                               {
                                   Text = x,
                                   Value = x,
                                   Selected = model.TimeZoneId == x
                               });
                    ModelState.AddModelError("foldererror", _sr["The selected folder name is already in use on another site."]);
                    return View(model);
                }
            }
            else
            {
                ISiteHost host;
                if (!string.IsNullOrEmpty(model.HostName))
                {
                    model.HostName = model.HostName.Replace("https://", string.Empty).Replace("http://", string.Empty);
                    host = await _siteManager.GetSiteHost(model.HostName);
                    if (host != null)
                    {
                        model.AllTimeZones = _tzHelper.GetTimeZoneList().Select(x =>
                               new SelectListItem
                               {
                                   Text = x,
                                   Value = x,
                                   Selected = model.TimeZoneId == x
                               });
                        ModelState.AddModelError("hosterror", _sr["The selected host/domain name is already in use on another site."]);
                        return View(model);
                    }
                    addHostName = true;
                }
            }
            
            // only the first site created by setup page should be a server admin site
            newSite.IsServerAdminSite = false;
            newSite.SiteName = model.SiteName;
            
            var siteNumber = 1 + await _siteManager.CountOtherSites(Guid.Empty);
            newSite.AliasId = $"s{siteNumber}";
            

            if (_multiTenantOptions.Mode == MultiTenantMode.FolderName)
            {
                newSite.SiteFolderName = model.SiteFolderName;
            }
            else if (addHostName)
            {
                newSite.PreferredHostName = model.HostName;
            }

            newSite.SiteIsClosed = model.IsClosed;
            newSite.SiteIsClosedMessage = model.ClosedMessage;
            
            await _siteManager.CreateNewSite(newSite);
            await _siteManager.CreateRequiredRolesAndAdminUser(
                newSite,
                model.Email,
                model.LoginName,
                model.DisplayName,
                model.Password
                );
            
            if (addHostName)
            {
                await _siteManager.AddHost(newSite.Id, model.HostName);
            }
            
            this.AlertSuccess(string.Format(_sr["Basic site settings for {0} were successfully created."],
                        newSite.SiteName), true);
            
            return RedirectToAction("SiteList", new { pageNumber = model.ReturnPageNumber });

        }

        [HttpPost]
        public async Task<JsonResult> AliasIdAvailable(Guid? siteId, string aliasId)
        {
            var selectedSiteId = Guid.Empty;
            if (siteId.HasValue) { selectedSiteId = siteId.Value; }
            bool available = await _siteManager.AliasIdIsAvailable(selectedSiteId, aliasId);
            return Json(available);
        }

        [HttpPost]
        public async Task<JsonResult> FolderNameAvailable(Guid? siteId, string siteFolderName)
        {           
            if(string.IsNullOrWhiteSpace(siteFolderName))
            {
                return Json(false);
            }
            var selectedSiteId = Guid.Empty;
            if (siteId.HasValue) { selectedSiteId = siteId.Value; }
            bool available = await _siteManager.FolderNameIsAvailable(selectedSiteId, siteFolderName);
            return Json(available);
        }

        [HttpPost]
        public async Task<JsonResult> HostNameAvailable(Guid? siteId, string hostName)
        {
            var selectedSiteId = Guid.Empty;
            if (siteId.HasValue) { selectedSiteId = siteId.Value; }
            bool available = await _siteManager.HostNameIsAvailable(selectedSiteId, hostName);
            return Json(available);
        }

        [HttpGet]
        [Authorize(Policy = "AdminPolicy")]
        public async Task<IActionResult> CompanyInfo(
            Guid? siteId,
            int slp = 1)
        {
            var selectedSite = await _siteManager.GetSiteForEdit(siteId);
            // only server admin site can edit other sites settings
            if (selectedSite.Id != _siteManager.CurrentSite.Id)
            {
                ViewData["Title"] = string.Format(CultureInfo.CurrentUICulture, _sr["{0} - Company Info"], selectedSite.SiteName);
            }
            else
            {
                ViewData["Title"] = _sr["Company Info"];
            }
            
            var model = new CompanyInfoViewModel();
            model.SiteId = selectedSite.Id;
            model.CompanyName = selectedSite.CompanyName;
            model.CompanyStreetAddress = selectedSite.CompanyStreetAddress;
            model.CompanyStreetAddress2 = selectedSite.CompanyStreetAddress2;
            model.CompanyLocality = selectedSite.CompanyLocality;
            model.CompanyRegion = selectedSite.CompanyRegion;
            model.CompanyPostalCode = selectedSite.CompanyPostalCode;
            model.CompanyCountry = selectedSite.CompanyCountry;
            model.CompanyPhone = selectedSite.CompanyPhone;
            model.CompanyFax = selectedSite.CompanyFax;
            model.CompanyPublicEmail = selectedSite.CompanyPublicEmail;
            model.CompanyWebsite = selectedSite.CompanyWebsite;

            model.AvailableCountries.Add(new SelectListItem { Text = _sr["-Please select-"], Value = "" });
            var countries = await _geoDataManager.GetAllCountries();
            var selectedCountryGuid = Guid.Empty;
            foreach (var country in countries)
            {
                if (country.ISOCode2 == model.CompanyCountry)
                {
                    selectedCountryGuid = country.Id;
                }
                model.AvailableCountries.Add(new SelectListItem()
                {
                    Text = country.Name,
                    Value = country.ISOCode2.ToString()
                });
            }

            if (selectedCountryGuid != Guid.Empty)
            {
                var states = await _geoDataManager.GetGeoZonesByCountry(selectedCountryGuid);
                foreach (var state in states)
                {
                    model.AvailableStates.Add(new SelectListItem()
                    {
                        Text = state.Name,
                        Value = state.Code
                    });
                }
            }

            return View(model);
        }

        // Post: /SiteAdmin/CompanyInfo
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminPolicy")]
        public async Task<ActionResult> CompanyInfo(CompanyInfoViewModel model)
        {
            var selectedSite = await _siteManager.GetSiteForEdit(model.SiteId);
            // only server admin site can edit other sites settings
            if (selectedSite.Id == _siteManager.CurrentSite.Id)
            {
                ViewData["Title"] = _sr["Company Info"];
            }
            else 
            {
                ViewData["Title"] = string.Format(CultureInfo.CurrentUICulture, _sr["{0} - Company Info"], selectedSite.SiteName);
            }

            if (selectedSite == null)
            {
                this.AlertDanger(_sr["oops something went wrong."], true);

                return RedirectToAction("Index");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (model.SiteId == Guid.Empty)
            {
                this.AlertDanger(_sr["oops something went wrong, site was not found."], true);

                return RedirectToAction("Index");
            }
            
            selectedSite.CompanyName = model.CompanyName;
            selectedSite.CompanyStreetAddress = model.CompanyStreetAddress;
            selectedSite.CompanyStreetAddress2 = model.CompanyStreetAddress2;
            selectedSite.CompanyLocality = model.CompanyLocality;
            selectedSite.CompanyRegion = model.CompanyRegion;
            selectedSite.CompanyPostalCode = model.CompanyPostalCode;
            selectedSite.CompanyCountry = model.CompanyCountry;
            selectedSite.CompanyPhone = model.CompanyPhone;
            selectedSite.CompanyFax = model.CompanyFax;
            selectedSite.CompanyPublicEmail = model.CompanyPublicEmail;
            selectedSite.CompanyWebsite = model.CompanyWebsite;

            await _siteManager.Update(selectedSite);
            
            this.AlertSuccess(string.Format(_sr["Company Info for {0} was successfully updated."],
                        selectedSite.SiteName), true);
            
            if ((_siteManager.CurrentSite.IsServerAdminSite)
                && (_siteManager.CurrentSite.Id != selectedSite.Id)
                )
            {
                return RedirectToAction("CompanyInfo", new { siteId = model.SiteId });
            }

            return RedirectToAction("CompanyInfo");
        }

        [HttpGet]
        [Authorize(Policy = "AdminPolicy")]
        public async Task<IActionResult> MailSettings(
            Guid? siteId,
            int slp = 1)
        {
            var selectedSite = await _siteManager.GetSiteForEdit(siteId);
            // only server admin site can edit other sites settings
            if (selectedSite.Id != _siteManager.CurrentSite.Id)
            {
                ViewData["Title"] = string.Format(CultureInfo.CurrentUICulture, _sr["{0} - Email Settings"], selectedSite.SiteName);
            }
            else
            {
                ViewData["Title"] = _sr["Email Settings"];
            }

            var model = new MailSettingsViewModel();
            model.SiteId = selectedSite.Id;
            model.DefaultEmailFromAddress = selectedSite.DefaultEmailFromAddress;
            model.DefaultEmailFromAlias = selectedSite.DefaultEmailFromAlias;
            model.SmtpPassword = selectedSite.SmtpPassword;
            model.SmtpPort = selectedSite.SmtpPort;
            model.SmtpPreferredEncoding = selectedSite.SmtpPreferredEncoding;
            model.SmtpRequiresAuth = selectedSite.SmtpRequiresAuth;
            model.SmtpServer = selectedSite.SmtpServer;
            model.SmtpUser = selectedSite.SmtpUser;
            model.SmtpUseSsl = selectedSite.SmtpUseSsl;
            
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminPolicy")]
        public async Task<ActionResult> MailSettings(MailSettingsViewModel model)
        {
            var selectedSite = await _siteManager.GetSiteForEdit(model.SiteId);
            // only server admin site can edit other sites settings
            if (selectedSite.Id == _siteManager.CurrentSite.Id)
            {
                ViewData["Title"] = _sr["Email Settings"];
            }
            else
            {
                ViewData["Title"] = string.Format(CultureInfo.CurrentUICulture, _sr["{0} - Email Settings"], selectedSite.SiteName);
            }

            if (selectedSite == null)
            {
                this.AlertDanger(_sr["oops something went wrong."], true);
                return RedirectToAction("Index");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (model.SiteId == Guid.Empty)
            {
                this.AlertDanger(_sr["oops something went wrong, site was not found."], true);
                return RedirectToAction("Index");
            }

            selectedSite.DefaultEmailFromAddress = model.DefaultEmailFromAddress;
            selectedSite.DefaultEmailFromAlias = model.DefaultEmailFromAlias;
            selectedSite.SmtpPassword = model.SmtpPassword;
            selectedSite.SmtpPort = model.SmtpPort;
            selectedSite.SmtpPreferredEncoding = model.SmtpPreferredEncoding;
            selectedSite.SmtpRequiresAuth = model.SmtpRequiresAuth;
            selectedSite.SmtpServer = model.SmtpServer;
            selectedSite.SmtpUser = model.SmtpUser;
            selectedSite.SmtpUseSsl = model.SmtpUseSsl;
            
            await _siteManager.Update(selectedSite);
            
            this.AlertSuccess(string.Format(_sr["Email Settings for {0} were successfully updated."],
                        selectedSite.SiteName), true);
            
            if ((_siteManager.CurrentSite.IsServerAdminSite)
                && (_siteManager.CurrentSite.Id != selectedSite.Id)
                )
            {
                return RedirectToAction("MailSettings", new { siteId = model.SiteId });
            }

            return RedirectToAction("MailSettings");
        }

        [HttpGet]
        [Authorize(Policy = "AdminPolicy")]
        public async Task<IActionResult> SmsSettings(
            Guid? siteId,
            int slp = 1)
        {

            // this is no longer used, previously sms was for 2fa, but now uses authenticator
            // currently using generic labels but only supports twilio

            var selectedSite = await _siteManager.GetSiteForEdit(siteId);
            // only server admin site can edit other sites settings
            if (selectedSite.Id != _siteManager.CurrentSite.Id)
            {
                ViewData["Title"] = string.Format(CultureInfo.CurrentUICulture, _sr["{0} - SMS Settings"], selectedSite.SiteName);
            }
            else
            {
                ViewData["Title"] = _sr["SMS Settings"];
            }

            var model = new SmsSettingsViewModel();
            model.SiteId = selectedSite.Id;
            model.SmsFrom = selectedSite.SmsFrom;
            model.SmsClientId = selectedSite.SmsClientId;
            model.SmsSecureToken = selectedSite.SmsSecureToken;
            
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminPolicy")]
        public async Task<ActionResult> SmsSettings(SmsSettingsViewModel model)
        {
            var selectedSite = await _siteManager.GetSiteForEdit(model.SiteId);
            // only server admin site can edit other sites settings
            if (selectedSite.Id == _siteManager.CurrentSite.Id)
            {
                ViewData["Title"] = _sr["SMS Settings"];
            }
            else 
            {
                ViewData["Title"] = string.Format(CultureInfo.CurrentUICulture, _sr["{0} - SMS Settings"], selectedSite.SiteName);
            }

            if (selectedSite == null)
            {
                this.AlertDanger(_sr["oops something went wrong."], true);
                return RedirectToAction("Index");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (model.SiteId == Guid.Empty)
            {
                this.AlertDanger(_sr["oops something went wrong, site was not found."], true);
                return RedirectToAction("Index");
            }
            
            selectedSite.SmsFrom = model.SmsFrom;
            selectedSite.SmsClientId = model.SmsClientId;
            selectedSite.SmsSecureToken = model.SmsSecureToken;
            
            await _siteManager.Update(selectedSite);
            
            this.AlertSuccess(string.Format(_sr["SMS Settings for {0} were successfully updated."],
                        selectedSite.SiteName), true);
            
            if ((_siteManager.CurrentSite.IsServerAdminSite)
                && (_siteManager.CurrentSite.Id != selectedSite.Id)
                )
            {
                return RedirectToAction("SmsSettings", new { siteId = model.SiteId });
            }

            return RedirectToAction("SmsSettings");
        }

        [HttpGet]
        [Authorize(Policy = "AdminPolicy")]
        public async Task<IActionResult> SecuritySettings(
            Guid? siteId,
            int slp = 1)
        {
            var selectedSite = await _siteManager.GetSiteForEdit(siteId);
            // only server admin site can edit other sites settings
            if (selectedSite.Id != _siteManager.CurrentSite.Id)
            {
                ViewData["Title"] = string.Format(CultureInfo.CurrentUICulture, _sr["{0} - Security Settings"], selectedSite.SiteName);
            }
            else
            {
                ViewData["Title"] = _sr["Security Settings"];
            }

            
            var model = new SecuritySettingsViewModel();
            model.SiteId = selectedSite.Id;
            model.AllowNewRegistration = selectedSite.AllowNewRegistration;
            model.AllowPersistentLogin = selectedSite.AllowPersistentLogin;
            model.DisableDbAuth = selectedSite.DisableDbAuth;
            model.ReallyDeleteUsers = selectedSite.ReallyDeleteUsers;
            model.RequireApprovalBeforeLogin = selectedSite.RequireApprovalBeforeLogin;
            model.RequireConfirmedEmail = selectedSite.RequireConfirmedEmail;
            model.UseEmailForLogin = selectedSite.UseEmailForLogin;
            model.RequireConfirmedPhone = selectedSite.RequireConfirmedPhone;
            model.AccountApprovalEmailCsv = selectedSite.AccountApprovalEmailCsv;

            model.EmailIsConfigured = await _siteCapabilities.SupportsEmailNotification(new SiteContext(selectedSite));
            model.SmsIsConfigured = selectedSite.SmsIsConfigured();
            model.HasAnySocialAuthEnabled = selectedSite.HasAnySocialAuthEnabled();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminPolicy")]
        public async Task<ActionResult> SecuritySettings(SecuritySettingsViewModel model)
        {
            var selectedSite = await _siteManager.GetSiteForEdit(model.SiteId);
            // only server admin site can edit other sites settings
            if (selectedSite.Id == _siteManager.CurrentSite.Id)
            {
                ViewData["Title"] = "Security Settings";
            }
            else if (_siteManager.CurrentSite.IsServerAdminSite)
            {
                ViewData["Title"] = string.Format(CultureInfo.CurrentUICulture, "{0} - Security Settings", selectedSite.SiteName);
            }

            if (selectedSite == null)
            {
                this.AlertDanger(_sr["oops something went wrong."], true);
                return RedirectToAction("Index");
            }

            if (model.SiteId == Guid.Empty)
            {
                this.AlertDanger(_sr["oops something went wrong, site was not found."], true);
                return RedirectToAction("Index");
            }
            
            if (!ModelState.IsValid)
            {
                model.EmailIsConfigured = await _siteCapabilities.SupportsEmailNotification(new SiteContext(selectedSite));
                model.SmsIsConfigured = selectedSite.SmsIsConfigured();
                model.HasAnySocialAuthEnabled = selectedSite.HasAnySocialAuthEnabled();
                return View(model);
            }
            
            selectedSite.AccountApprovalEmailCsv = model.AccountApprovalEmailCsv;
            selectedSite.AllowNewRegistration = model.AllowNewRegistration;
            selectedSite.AllowPersistentLogin = model.AllowPersistentLogin;
            selectedSite.DisableDbAuth = model.DisableDbAuth;
            selectedSite.ReallyDeleteUsers = model.ReallyDeleteUsers;
            selectedSite.RequireApprovalBeforeLogin = model.RequireApprovalBeforeLogin;
            selectedSite.RequireConfirmedEmail = model.RequireConfirmedEmail;
            selectedSite.RequireConfirmedPhone = model.RequireConfirmedPhone;
            selectedSite.UseEmailForLogin = model.UseEmailForLogin;
            
            await _siteManager.Update(selectedSite);
            
            this.AlertSuccess(string.Format(_sr["Security Settings for {0} was successfully updated."],
                        selectedSite.SiteName), true);
            
            if ((_siteManager.CurrentSite.IsServerAdminSite)
                && (_siteManager.CurrentSite.Id != selectedSite.Id)
                )
            {
                return RedirectToAction("SecuritySettings", new { siteId = model.SiteId });
            }

            return RedirectToAction("SecuritySettings");
        }

        [HttpGet]
        [Authorize(Policy = "AdminPolicy")]
        public async Task<IActionResult> Captcha(
            Guid? siteId,
            int slp = 1)
        {
            var selectedSite = await _siteManager.GetSiteForEdit(siteId);
            // only server admin site can edit other sites settings
            if (selectedSite.Id != _siteManager.CurrentSite.Id)
            {
                ViewData["Title"] = string.Format(CultureInfo.CurrentUICulture, _sr["{0} - Captcha Settings"], selectedSite.SiteName);
            }
            else
            {
                ViewData["Title"] = _sr["Captcha Settings"];
            }
            
            var model = new CaptchaSettingsViewModel();
            model.SiteId = selectedSite.Id;
            model.RecaptchaPrivateKey = selectedSite.RecaptchaPrivateKey;
            model.RecaptchaPublicKey = selectedSite.RecaptchaPublicKey;
            model.UseInvisibleCaptcha = selectedSite.UseInvisibleRecaptcha;
            model.RequireCaptchaOnLogin = selectedSite.CaptchaOnLogin;
            model.RequireCaptchaOnRegistration = selectedSite.CaptchaOnRegistration;

            return View("CaptchaSettings", model);
        }

        // Post: /SiteAdmin/Captcha
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminPolicy")]
        public async Task<ActionResult> Captcha(CaptchaSettingsViewModel model)
        {
            var selectedSite = await _siteManager.GetSiteForEdit(model.SiteId);
            // only server admin site can edit other sites settings
            if (selectedSite.Id == _siteManager.CurrentSite.Id)
            {
                ViewData["Title"] = _sr["Captcha Settings"];
            }
            else 
            {
                ViewData["Title"] = string.Format(CultureInfo.CurrentUICulture, _sr["{0} - Captcha Settings"], selectedSite.SiteName);
            }

            if (selectedSite == null)
            {
                this.AlertDanger(_sr["oops something went wrong."], true);
                return RedirectToAction("Index");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (model.SiteId == Guid.Empty)
            {
                this.AlertDanger(_sr["oops something went wrong, site was not found."], true);
                return RedirectToAction("Index");
            }

            selectedSite.RecaptchaPublicKey = model.RecaptchaPublicKey;
            selectedSite.RecaptchaPrivateKey = model.RecaptchaPrivateKey;
            selectedSite.UseInvisibleRecaptcha = model.UseInvisibleCaptcha;
            selectedSite.CaptchaOnRegistration = model.RequireCaptchaOnRegistration;
            selectedSite.CaptchaOnLogin = model.RequireCaptchaOnLogin;

            await _siteManager.Update(selectedSite);
            
            this.AlertSuccess(string.Format(_sr["Captcha Settings for {0} was successfully updated."],
                        selectedSite.SiteName), true);
            
            if ((_siteManager.CurrentSite.IsServerAdminSite)
                &&(_siteManager.CurrentSite.Id != selectedSite.Id)
                )
            {
                return RedirectToAction("Captcha", new { siteId = model.SiteId });
            }

            return RedirectToAction("Captcha");
        }

        // GET: /SiteAdmin/SocialLogins
        [HttpGet]
        [Authorize(Policy = "AdminPolicy")]
        public async Task<IActionResult> SocialLogins(
            Guid? siteId,
            int slp = 1)
        {
            var selectedSite = await _siteManager.GetSiteForEdit(siteId);
            // only server admin site can edit other sites settings
            if (selectedSite.Id != _siteManager.CurrentSite.Id)
            {
                ViewData["Title"] = string.Format(CultureInfo.CurrentUICulture, _sr["{0} - Social Login Settings"], selectedSite.SiteName);
            }
            else
            {
                ViewData["Title"] = _sr["Social Login Settings"];
            }
            
            var model = new SocialLoginSettingsViewModel();
            model.SiteId = selectedSite.Id;
            model.FacebookAppId = selectedSite.FacebookAppId;
            model.FacebookAppSecret = selectedSite.FacebookAppSecret;
            model.GoogleClientId = selectedSite.GoogleClientId;
            model.GoogleClientSecret = selectedSite.GoogleClientSecret;
            model.MicrosoftClientId = selectedSite.MicrosoftClientId;
            model.MicrosoftClientSecret = selectedSite.MicrosoftClientSecret;
            model.TwitterConsumerKey = selectedSite.TwitterConsumerKey;
            model.TwitterConsumerSecret = selectedSite.TwitterConsumerSecret;
            model.OidConnectDisplayName = selectedSite.OidConnectDisplayName;
            model.OidConnectAppId = selectedSite.OidConnectAppId;
            model.OidConnectAppSecret = selectedSite.OidConnectAppSecret;
            model.OidConnectAuthority = selectedSite.OidConnectAuthority;

            return View(model);

        }

        // Post: /SiteAdmin/SocialLogins
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminPolicy")]
        public async Task<ActionResult> SocialLogins(SocialLoginSettingsViewModel model)
        {
            var selectedSite = await _siteManager.GetSiteForEdit(model.SiteId);
            // only server admin site can edit other sites settings
            if (selectedSite.Id == _siteManager.CurrentSite.Id)
            {
                ViewData["Title"] = _sr["Social Login Settings"];
            }
            else if (_siteManager.CurrentSite.IsServerAdminSite)
            {
                ViewData["Title"] = string.Format(CultureInfo.CurrentUICulture, _sr["{0} - Social Login Settings"], selectedSite.SiteName);
            }

            if (selectedSite == null)
            {
                this.AlertDanger(_sr["oops something went wrong."], true);
                return RedirectToAction("Index");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (model.SiteId == Guid.Empty)
            {
                this.AlertDanger(_sr["oops something went wrong, site was not found."], true);
                return RedirectToAction("Index");
            }
            
            selectedSite.FacebookAppId = model.FacebookAppId;
            selectedSite.FacebookAppSecret = model.FacebookAppSecret;
            selectedSite.GoogleClientId = model.GoogleClientId;
            selectedSite.GoogleClientSecret = model.GoogleClientSecret;
            selectedSite.MicrosoftClientId = model.MicrosoftClientId;
            selectedSite.MicrosoftClientSecret = model.MicrosoftClientSecret;
            selectedSite.TwitterConsumerKey = model.TwitterConsumerKey;
            selectedSite.TwitterConsumerSecret = model.TwitterConsumerSecret;
            selectedSite.OidConnectDisplayName = model.OidConnectDisplayName;
            selectedSite.OidConnectAppId = model.OidConnectAppId;
            selectedSite.OidConnectAppSecret = model.OidConnectAppSecret;
            selectedSite.OidConnectAuthority = model.OidConnectAuthority;

            await _siteManager.Update(selectedSite);
            //TODO: need to wrap ICache into something more abstract and/or move it into sitemanager
            // also need to clear using the folder name if it isn't root site or hostname if using tenants per host
           // cache.Remove("root");
            
            this.AlertSuccess(string.Format(_sr["Social Login Settings for {0} was successfully updated."],
                        selectedSite.SiteName), true);
            
            if ((_siteManager.CurrentSite.IsServerAdminSite)
                && (_siteManager.CurrentSite.Id != selectedSite.Id)
                )
            {
                return RedirectToAction("SocialLogins", new { siteId = model.SiteId });
            }

            return RedirectToAction("SocialLogins");
        }

        // GET: /SiteAdmin/LoginPageInfo
        [HttpGet]
        [Authorize(Policy = "AdminPolicy")]
        public async Task<IActionResult> LoginPageInfo(
            Guid? siteId,
            int slp = 1)
        {
            var selectedSite = await _siteManager.GetSiteForEdit(siteId);
            // only server admin site can edit other sites settings
            if (selectedSite.Id != _siteManager.CurrentSite.Id)
            {
                ViewData["Title"] = string.Format(CultureInfo.CurrentUICulture, _sr["{0} - Login Page Content"], selectedSite.SiteName);
            }
            else
            {
                ViewData["Title"] = _sr["Login Page Content"];
            }
            
            var model = new LoginInfoViewModel();
            model.SiteId = selectedSite.Id;
            model.LoginInfoTop = selectedSite.LoginInfoTop;
            model.LoginInfoBottom = selectedSite.LoginInfoBottom;
            
            return View(model);
        }

        // Post: /SiteAdmin/LoginPageInfo
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminPolicy")]
        public async Task<ActionResult> LoginPageInfo(LoginInfoViewModel model)
        {
            var selectedSite = await _siteManager.GetSiteForEdit(model.SiteId);
            // only server admin site can edit other sites settings
            if (selectedSite.Id == _siteManager.CurrentSite.Id)
            {
                ViewData["Title"] = _sr["Login Page Content"];
            }
            else if (_siteManager.CurrentSite.IsServerAdminSite)
            {
                ViewData["Title"] = string.Format(CultureInfo.CurrentUICulture, _sr["{0} - Login Page Content"], selectedSite.SiteName);
            }

            if (selectedSite == null)
            {
                this.AlertDanger(_sr["oops something went wrong."], true);
                return RedirectToAction("Index");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (model.SiteId == Guid.Empty)
            {
                this.AlertDanger(_sr["oops something went wrong, site was not found."], true);
                return RedirectToAction("Index");
            }
            
            selectedSite.LoginInfoTop = model.LoginInfoTop;
            selectedSite.LoginInfoBottom = model.LoginInfoBottom;
            
            await _siteManager.Update(selectedSite);
            
            this.AlertSuccess(string.Format(_sr["Login Page Info for {0} was successfully updated."],
                        selectedSite.SiteName), true);
            
            if ((_siteManager.CurrentSite.IsServerAdminSite)
                && (_siteManager.CurrentSite.Id != selectedSite.Id)
                )
            {
                return RedirectToAction("LoginPageInfo", new { siteId = model.SiteId });
            }

            return RedirectToAction("LoginPageInfo");
        }

        // GET: /SiteAdmin/RegisterPageInfo
        [HttpGet]
        [Authorize(Policy = "AdminPolicy")]
        public async Task<IActionResult> RegisterPageInfo(
            Guid? siteId,
            int slp = 1)
        {
            var selectedSite = await _siteManager.GetSiteForEdit(siteId);
            // only server admin site can edit other sites settings
            if (selectedSite.Id != _siteManager.CurrentSite.Id)
            {
                ViewData["Title"] = string.Format(CultureInfo.CurrentUICulture, _sr["{0} - Registration Page Content"], selectedSite.SiteName);
            }
            else
            {
                ViewData["Title"] = _sr["Registration Page Content"];
            }
            
            var model = new RegisterInfoViewModel();
            model.SiteId = selectedSite.Id;
            model.RegistrationPreamble = selectedSite.RegistrationPreamble;
            model.RegistrationAgreement = selectedSite.RegistrationAgreement;
            
            return View(model);
        }


        // Post: /SiteAdmin/RegisterPageInfo
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminPolicy")]
        public async Task<ActionResult> RegisterPageInfo(RegisterInfoViewModel model)
        {
            var selectedSite = await _siteManager.GetSiteForEdit(model.SiteId);
            // only server admin site can edit other sites settings
            if (selectedSite.Id == _siteManager.CurrentSite.Id)
            {
                ViewData["Title"] = _sr["Registration Page Content"];
            }
            else if (_siteManager.CurrentSite.IsServerAdminSite)
            {
                ViewData["Title"] = string.Format(CultureInfo.CurrentUICulture, _sr["{0} - Registration Page Content"], selectedSite.SiteName);
            }

            if (selectedSite == null)
            {
                this.AlertDanger(_sr["oops something went wrong."], true);
                return RedirectToAction("Index");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (model.SiteId == Guid.Empty)
            {
                this.AlertDanger(_sr["oops something went wrong, site was not found."], true);
                return RedirectToAction("Index");
            }

            if(selectedSite.RegistrationAgreement != model.RegistrationAgreement)
            {
                if(model.RequireUsersToAcceptChangedAgreement)
                {
                    // changing the terms date will force non admin users to re-accept the terms
                    selectedSite.TermsUpdatedUtc = DateTime.UtcNow;
                }
                
            }
            
            selectedSite.RegistrationPreamble = model.RegistrationPreamble;
            selectedSite.RegistrationAgreement = model.RegistrationAgreement;
            
            await _siteManager.Update(selectedSite);
            
            this.AlertSuccess(string.Format(_sr["Registration Page Content for {0} was successfully updated."],
                        selectedSite.SiteName), true);
            
            if ((_siteManager.CurrentSite.IsServerAdminSite)
                && (_siteManager.CurrentSite.Id != selectedSite.Id)
                )
            {
                return RedirectToAction("RegisterPageInfo", new { siteId = model.SiteId });
            }

            return RedirectToAction("RegisterPageInfo");
        }


        // probably should hide delete by config by default?

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "ServerAdminPolicy")]
        public async Task<ActionResult> SiteDelete(Guid siteId, int returnPageNumber = 1)
        {
            var selectedSite = await _siteManager.Fetch(siteId);

            if (selectedSite != null)
            {

                if (selectedSite.IsServerAdminSite)
                {
                    this.AlertWarning(string.Format(
                            _sr["The site {0} was not deleted because it is a server admin site."],
                            selectedSite.SiteName)
                            , true);

                    return RedirectToAction("SiteList", new { pageNumber = returnPageNumber });
                }

                await _siteManager.Delete(selectedSite);

                this.AlertWarning(string.Format(
                            _sr["The site {0} was successfully deleted."],
                            selectedSite.SiteName)
                            , true);
            }
            
            return RedirectToAction("SiteList", new { pageNumber = returnPageNumber });
        }

        [HttpGet]
        [Authorize(Policy = "ServerAdminPolicy")]
        public async Task<ActionResult> SiteHostMappings(
            Guid? siteId,
            int slp = -1)
        {
            var selectedSite = await _siteManager.GetSiteForEdit(siteId);
            // only server admin site can edit other sites settings
            if (selectedSite.Id != _siteManager.CurrentSite.Id)
            {
                ViewData["Title"] = string.Format(CultureInfo.InvariantCulture,
                _sr["Domain/Host Name Mappings for {0}"],
                selectedSite.SiteName);
            }
            else
            {
                ViewData["Title"] = _sr["Domain/Host Name Mappings"];
            }

            var model = new SiteHostMappingsViewModel();
            model.SiteId = selectedSite.Id;
            model.HostMappings = await _siteManager.GetSiteHosts(selectedSite.Id);
            if (slp > -1)
            {
                model.SiteListReturnPageNumber = slp;
            }

            return View("SiteHosts", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "ServerAdminPolicy")]
        public async Task<ActionResult> HostAdd(
            Guid siteId,
            string hostName,
            int slp = -1)
        {
            var selectedSite = await _siteManager.Fetch(siteId);

            if (selectedSite == null)
            {
                return RedirectToAction("Index");
            }

            if(selectedSite.Id != _siteManager.CurrentSite.Id && !_siteManager.CurrentSite.IsServerAdminSite)
            {
                return RedirectToAction("Index");
            }

            ISiteHost host;
            if (!string.IsNullOrEmpty(hostName))
            {
                hostName = hostName.Replace("https://", string.Empty).Replace("http://", string.Empty);
                host = await _siteManager.GetSiteHost(hostName);

                if (host != null)
                {
                    if (host.SiteId != selectedSite.Id)
                    {
                        this.AlertWarning(
                        _sr["failed to add the requested host name mapping becuase it is already mapped to another site."],
                        true);
                    }
                }
                else
                {
                    await _siteManager.AddHost(selectedSite.Id, hostName);
                    
                    this.AlertSuccess(string.Format(_sr["Host/domain mapping for {0} was successfully created."],
                                selectedSite.SiteName), true);
                }
            }

            return RedirectToAction("SiteHostMappings", new { siteId = siteId, slp = slp });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "ServerAdminPolicy")]
        public async Task<ActionResult> HostDelete(
            Guid siteId,
            string hostName,
            int slp = -1)
        {
            var selectedSite = await _siteManager.Fetch(siteId);

            if (selectedSite == null)
            {
                return RedirectToAction("Index");
            }

            if (selectedSite.Id != _siteManager.CurrentSite.Id && !_siteManager.CurrentSite.IsServerAdminSite)
            {
                return RedirectToAction("Index");
            }

            var host = await _siteManager.GetSiteHost(hostName);
            
            if (host != null)
            {
                if (host.SiteId == selectedSite.Id)
                {
                    if (selectedSite.PreferredHostName == host.HostName)
                    {
                        selectedSite.PreferredHostName = string.Empty;
                        await _siteManager.Update(selectedSite);
                    }

                    await _siteManager.DeleteHost(host.SiteId, host.Id);
                    
                    this.AlertSuccess(string.Format(_sr["Host/domain mapping for {0} was successfully removed."],
                                selectedSite.SiteName), true);
                }
            }

            return RedirectToAction("SiteHostMappings", new { siteId = siteId, slp = slp });
        }
  
    }
}
