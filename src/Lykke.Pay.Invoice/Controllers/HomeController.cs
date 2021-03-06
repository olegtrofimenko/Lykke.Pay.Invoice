﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Lykke.AzureRepositories;
using Lykke.AzureRepositories.Azure.Tables;
using Lykke.Core;
using Lykke.Pay.Common.Entities.Entities;
using Lykke.Pay.Invoice.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace Lykke.Pay.Invoice.Controllers
{
    [Route("home")]
    public class HomeController : BaseController
    {


        public HomeController(IConfiguration configuration) : base(configuration)
        {

        }



        [HttpGet("welcome")]
        public IActionResult Welcome(string returnUrl)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }


        [HttpPost("welcome")]
        public async Task<IActionResult> Welcome(MerchantStaffSignInRequest request, string returnUrl)
        {
            //var request = new MerchantStaffSignInRequest
            //{
            //    Login = login,
            //    Password = password
            //};
            if (string.IsNullOrEmpty(request.Login) || string.IsNullOrEmpty(request.Password))
            {
                return View();
            }
            var httpClient = new HttpClient();
            var response = await httpClient.PostAsync(MerchantAuthService, new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json"));
            var rstText = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(rstText))
            {
                return View();
            }


            var user = JsonConvert.DeserializeObject<MerchantStaff>(rstText);

            if (user == null)
            {
                return View();
            }


            var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Sid, user.MerchantStaffEmail),
                    new Claim(ClaimTypes.UserData, rstText),
                    new Claim(ClaimTypes.Name, $"{user.MerchantStaffFirstName} {user.MerchantStaffLastName}")
                };

            var claimsIdentity = new ClaimsIdentity(claims, "password");
            var claimsPrinciple = new ClaimsPrincipal(claimsIdentity);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrinciple);

            return Redirect(Url.IsLocalUrl(returnUrl) ? returnUrl : HomeUrl);

        }




        [HttpGet("SignOut")]
        public async Task<IActionResult> SignOut()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Redirect(HomeUrl);
        }


    }
}

