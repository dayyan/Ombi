#region Copyright

// /************************************************************************
//    Copyright (c) 2016 Jamie Rees
//    File: LoginModule.cs
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

using System;
using System.Dynamic;
using System.Security;
using Nancy;
using Nancy.Extensions;
using Nancy.Linker;
using Nancy.Responses.Negotiation;
using Nancy.Security;
using Ombi.Core;
using Ombi.Core.SettingModels;
using Ombi.Helpers;
using Ombi.Helpers.Permissions;
using Ombi.Store;
using Ombi.Store.Repository;
using Ombi.UI.Authentication;
using Ombi.UI.Models;
using ISecurityExtensions = Ombi.Core.ISecurityExtensions;

namespace Ombi.UI.Modules
{
    public class LoginModule : BaseModule
    {
        public LoginModule(ISettingsService<PlexRequestSettings> pr, ICustomUserMapper m, IResourceLinker linker, IRepository<UserLogins> userLoginRepo, ISecurityExtensions security)
            : base(pr, security)
        {
            UserMapper = m;
            Get["LocalLogin","/login"] = _ =>
            {
                if (LoggedIn)
                {
                    var url = linker.BuildRelativeUri(Context, "SearchIndex");
                    return Response.AsRedirect(url.ToString());
                }
                dynamic model = new ExpandoObject();
                model.Redirect = Request.Query.redirect.Value ?? string.Empty;
                model.Errored = Request.Query.error.HasValue;
                var adminCreated = UserMapper.DoUsersExist();
                model.AdminExists = adminCreated;
                return View["Index", model];
            };

            Get["/logout"] = x =>
            {
                if (Session[SessionKeys.UsernameKey] != null)
                {
                    Session.Delete(SessionKeys.UsernameKey);
                }
                return CustomModuleExtensions.LogoutAndRedirect(this, !string.IsNullOrEmpty(BaseUrl) ? $"~/{BaseUrl}/" : "~/");
            };

            Post["/login"] = x =>
            {
                var username = (string)Request.Form.Username;
                var password = (string)Request.Form.Password;
                var dtOffset = (int)Request.Form.DateTimeOffset;
                var redirect = (string)Request.Form.Redirect;

                var userId = UserMapper.ValidateUser(username, password);

                if (userId == null)
                {
                    return
                        Context.GetRedirect(!string.IsNullOrEmpty(BaseUrl)
                            ? $"~/{BaseUrl}/login?error=true&username=" + username
                            : "~/login?error=true&username=" + username);
                }
                DateTime? expiry = null;
                if (Request.Form.RememberMe.HasValue)
                {
                    expiry = DateTime.Now.AddDays(7);
                }
                Session[SessionKeys.UsernameKey] = username;
                Session[SessionKeys.ClientDateTimeOffsetKey] = dtOffset;
                if (redirect.Contains("userlogin"))
                {
                    redirect = !string.IsNullOrEmpty(BaseUrl) ? $"/{BaseUrl}/search" : "/search";
                }

                userLoginRepo.Insert(new UserLogins
                {
                    LastLoggedIn = DateTime.UtcNow,
                    Type = UserType.LocalUser,
                    UserId = userId.ToString()
                });

                return CustomModuleExtensions.LoginAndRedirect(this,userId.Value, expiry, redirect);
            };

            Get["/register"] = x =>
            {
                {
                    dynamic model = new ExpandoObject();
                    model.Errored = Request.Query.error.HasValue;

                    return View["Register", model];
                }
            };

            Post["/register"] = x =>
            {
                var username = (string)Request.Form.Username;
                var exists = UserMapper.DoUsersExist();
                if (exists)
                {
                    return
                        Context.GetRedirect(!string.IsNullOrEmpty(BaseUrl)
                            ? $"~/{BaseUrl}/register?error=true"
                            : "~/register?error=true");
                }
                var userId = UserMapper.CreateUser(username, Request.Form.Password, EnumHelper<Permissions>.All(), 0);
                Session[SessionKeys.UsernameKey] = username;
                return CustomModuleExtensions.LoginAndRedirect(this, (Guid)userId);
            };

            Get["/changepassword"] = _ => ChangePassword();
            Post["/changepassword"] = _ => ChangePasswordPost();
        }

        private ICustomUserMapper UserMapper { get; }

        private Negotiator ChangePassword()
        {
            this.RequiresAuthentication();
            return View["ChangePassword"];
        }

        private Response ChangePasswordPost()
        {
            var username = Context.CurrentUser.UserName;
            var oldPass = Request.Form.OldPassword;
            var newPassword = Request.Form.NewPassword;
            var newPasswordAgain = Request.Form.NewPasswordAgain;

            if (string.IsNullOrEmpty(oldPass) || string.IsNullOrEmpty(newPassword) ||
                string.IsNullOrEmpty(newPasswordAgain))
            {
                return Response.AsJson(new JsonResponseModel { Message = "Please fill in all fields", Result = false });
            }

            if (!newPassword.Equals(newPasswordAgain))
            {
                return Response.AsJson(new JsonResponseModel { Message = "The passwords do not match", Result = false });
            }
            try
            {
                var result = UserMapper.UpdatePassword(username, oldPass, newPassword);
                if (result)
                {
                    return Response.AsJson(new JsonResponseModel { Message = "Password has been changed!", Result = true });
                }

                return Response.AsJson(new JsonResponseModel { Message = "Could not update the password in the database", Result = false });
            }
            catch (SecurityException e)
            {
                return Response.AsJson(new JsonResponseModel { Message = e.ToString(), Result = false });
            }
        }
    }
}