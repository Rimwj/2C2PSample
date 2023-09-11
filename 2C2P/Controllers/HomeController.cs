using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Web;
using System.Web.Mvc;

namespace _2C2P.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }


        [HttpPost]
        public void Test(string cardnumber) {
            var encCardData = System.Web.HttpContext.Current.Request.Params["encryptedCardInfo"];
            var maskedCardNo = System.Web.HttpContext.Current.Request.Params["maskedCardInfo"];
            var expMonth = System.Web.HttpContext.Current.Request.Params["expMonthCardInfo"];
            var expYear = System.Web.HttpContext.Current.Request.Params["expYearCardInfo"];
        }


        private string GetPaymentToken()
        {
            string secretKey = "test";
            byte[] keyByteArray = Encoding.UTF8.GetBytes(secretKey);

            SymmetricSecurityKey symmetricSecurityKey = new SymmetricSecurityKey(keyByteArray);

            SigningCredentials credentials = new SigningCredentials(symmetricSecurityKey, SecurityAlgorithms.HmacSha256Signature);
            List<Claim> claims = new List<Claim>();
            var jsonString = JsonConvert.SerializeObject("paymentchannel");

            claims.Add(new Claim("merchantID", "test"));
            claims.Add(new Claim("invoiceNo", "InNo1234567890"));
            claims.Add(new Claim("description", "testdec"));
            claims.Add(new Claim("amount", "1.0", ClaimValueTypes.Double));
            claims.Add(new Claim("currencyCode", "TH"));
            //claims.Add(new Claim("nonceStr", input.nonceStr));
            claims.Add(new Claim("paymentChannel", "QR", JsonClaimValueTypes.JsonArray));
            claims.Add(new Claim("request3DS", "test"));

            JwtSecurityToken token = new JwtSecurityToken
            (
                issuer: null,
                audience: null,
                claims: claims,
                expires: null,
                signingCredentials: credentials
            );
            //JWT Token 생성
            string tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            //JWT Token으로 2C2P payment토큰 발행
            var clientForPaymentToken = new RestClient("https://sandbox-pgw.2c2p.com/payment/4.1/PaymentToken");
            var requestForPaymentToken = new RestRequest("", Method.POST);
            requestForPaymentToken.AddHeader("accept", "application/json");
            requestForPaymentToken.AddHeader("content-type", "application/*+json");
            requestForPaymentToken.AddParameter("application/*+json", "{\"payload\":\"" + tokenString + "\"}", ParameterType.RequestBody);
            IRestResponse paymentTokenResponse = clientForPaymentToken.Execute(requestForPaymentToken);

            Response2C2P paymentTokenDto = JsonConvert.DeserializeObject<Response2C2P>(paymentTokenResponse.Content);

            var stream = paymentTokenDto.payload;
            var handle = new JwtSecurityTokenHandler();
            JwtSecurityToken jsonTokenResult = (JwtSecurityToken)handle.ReadToken(stream);
            string paymentToken = jsonTokenResult.Payload.FirstOrDefault(x => x.Key == "paymentToken").Value.ToString(); // 발급받은 토큰

            return paymentToken;
        }

    }
}