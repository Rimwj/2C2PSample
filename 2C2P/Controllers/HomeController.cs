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
using JWT;
using JWT.Algorithms;
using JWT.Serializers;
using JWT.Exceptions;
using System.Net;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Logging;
using Antlr.Runtime.Misc;

namespace _2C2P.Controllers
{
    public class HomeController : Controller
    {
        public string _paymentToken;
        public string _clientId;

        public string _merchantID = "JT04";
        public string _invoiceNo;
        public string _description = "Item 1";
        public double _amount = 10;
        public string _currency = "THB";
        public string _secretKey = "CD229682D3297390B9F66FF4020B758F4A5E625AF4992E5D75D311D6458B38E2";
        public List<string> _paymentChannel;

        public ResponsePaymentOption _paymentOption;
        public ResponsePaymentOptionDetail _paymentOptionDetail;
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

            _clientId = Guid.NewGuid().ToString();
            var paymentToken = GeneratePaymentToken();
            var paymentOption = GeneratePaymentOption();
            var step6 = PaymentOptionDetail();

            DoPayment(encCardData);
            CheckPayment();
        }

        public string GeneratePaymentToken()
        {
            _invoiceNo = DateTime.UtcNow.Ticks.ToString();
            _paymentChannel = new List<string>()
            {
                "CC"
            };
            string secretKey = _secretKey;
            byte[] keyByteArray = Encoding.UTF8.GetBytes(secretKey);

            SymmetricSecurityKey symmetricSecurityKey = new SymmetricSecurityKey(keyByteArray);

            SigningCredentials credentials = new SigningCredentials(symmetricSecurityKey, SecurityAlgorithms.HmacSha256);
            List<Claim> claims = new List<Claim>();
            var tempStringList = new List<string>();
            tempStringList.Add("QR");
            var jsonString = JsonConvert.SerializeObject(_paymentChannel);

            claims.Add(new Claim("merchantID", _merchantID));
            claims.Add(new Claim("invoiceNo", _invoiceNo));
            claims.Add(new Claim("description", _description));
            claims.Add(new Claim("amount", _amount.ToString(), ClaimValueTypes.Double));
            claims.Add(new Claim("currencyCode", _currency));
            claims.Add(new Claim("paymentChannel", jsonString, JsonClaimValueTypes.JsonArray));
            claims.Add(new Claim("request3DS", "N"));

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
            var requestForPaymentToken = new RestRequest(Method.POST);
            requestForPaymentToken.AddHeader("accept", "application/json");
            requestForPaymentToken.AddHeader("content-type", "application/*+json");
            requestForPaymentToken.AddParameter("application/*+json", "{\"payload\":\"" + tokenString + "\"}", ParameterType.RequestBody);
            IRestResponse paymentTokenResponse = clientForPaymentToken.Execute(requestForPaymentToken);
            Response2C2P paymentTokenDto = JsonConvert.DeserializeObject<Response2C2P>(paymentTokenResponse.Content);

            var stream = paymentTokenDto.payload;
            var handle = new JwtSecurityTokenHandler();
            JwtSecurityToken jsonTokenResult = (JwtSecurityToken)handle.ReadToken(stream);
            string paymentToken = jsonTokenResult.Payload.FirstOrDefault(x => x.Key == "paymentToken").Value.ToString(); // 발급받은 토큰
            _paymentToken = paymentToken;
            return paymentToken;

        }

        public ResponsePaymentOption GeneratePaymentOption()
        {
            var tempRequest = new
            {
                paymentToken = _paymentToken
            };
            //발급 받은 토큰으로 QR 코드 생성
            string request = JsonConvert.SerializeObject(tempRequest);

            var clientForQr = new RestClient("https://sandbox-pgw.2c2p.com/payment/4.1/paymentOption");
            var requestForQr = new RestRequest(Method.POST);
            requestForQr.AddHeader("accept", "application/json");
            requestForQr.AddHeader("content-type", "application/*+json");
            requestForQr.AddParameter("application/*+json", request, ParameterType.RequestBody);
            IRestResponse qrResponse = clientForQr.Execute(requestForQr);

            ResponsePaymentOption qrResponseDto = JsonConvert.DeserializeObject<ResponsePaymentOption>(qrResponse.Content);
            if (qrResponseDto?.respCode == null)
            {
                throw new Exception(qrResponseDto.respDesc);
            }
            _paymentOption = qrResponseDto;
            return qrResponseDto;
        }
        public string PaymentOptionDetail()
        {
            var tempRequest = new
            {
                categoryCode = _paymentOption.channelCategories.FirstOrDefault().code,
                groupcode = _paymentOption.channelCategories.FirstOrDefault().groups.FirstOrDefault().code,
                paymentToken = _paymentToken
            };
            //발급 받은 토큰으로 QR 코드 생성
            string request = JsonConvert.SerializeObject(tempRequest);

            var clientForQr = new RestClient("https://sandbox-pgw.2c2p.com/payment/4.1/paymentOptionDetails");
            var requestForQr = new RestRequest(Method.POST);
            requestForQr.AddHeader("accept", "application/json");
            requestForQr.AddHeader("content-type", "application/*+json");
            requestForQr.AddParameter("application/*+json", request, ParameterType.RequestBody);
            IRestResponse qrResponse = clientForQr.Execute(requestForQr);

            ResponsePaymentOptionDetail qrResponseDto = JsonConvert.DeserializeObject<ResponsePaymentOptionDetail>(qrResponse.Content);
            if (qrResponseDto?.respCode == null)
            {
                throw new Exception(qrResponseDto.respDesc);
            }
            _paymentOptionDetail = qrResponseDto;
            return null;
        }

        public void DoPayment(string encCardData)
        {
            var tempRequest = new
            {
                paymentToken = _paymentToken,
                payment = new
                {
                    code = new
                    {
                        channelCode = _paymentOptionDetail.groupCode
                    },
                    data = new
                    {
                        name = "wjlim",
                        email = "wjlim@inventis.co.kr",
                        securePayToken = encCardData
                    }
                }
            };

            string request = JsonConvert.SerializeObject(tempRequest);

            var clientForQr = new RestClient("https://sandbox-pgw.2c2p.com/payment/4.1/payment");
            var requestForQr = new RestRequest(Method.POST);
            requestForQr.AddHeader("accept", "application/json");
            requestForQr.AddHeader("content-type", "application/*+json");
            requestForQr.AddParameter("application/*+json", request, ParameterType.RequestBody);
            IRestResponse qrResponse = clientForQr.Execute(requestForQr);

            ResponsePaymentOptionDetail qrResponseDto = JsonConvert.DeserializeObject<ResponsePaymentOptionDetail>(qrResponse.Content);
            if (qrResponseDto?.respCode == null)
            {
                throw new Exception(qrResponseDto.respDesc);
            }
            //_paymentOptionDetail = qrResponseDto;
        }
        public void CheckPayment()
        {
            var tempRequest = new
            {
                paymentToken = _paymentToken
            };

            string request = JsonConvert.SerializeObject(tempRequest);

            var clientForQr = new RestClient("https://sandbox-pgw.2c2p.com/payment/4.1/transactionStatus");
            var requestForQr = new RestRequest(Method.POST);
            requestForQr.AddHeader("accept", "application/json");
            requestForQr.AddHeader("content-type", "application/*+json");
            requestForQr.AddParameter("application/*+json", request, ParameterType.RequestBody);
            IRestResponse qrResponse = clientForQr.Execute(requestForQr);

            ResponsePaymentOptionDetail qrResponseDto = JsonConvert.DeserializeObject<ResponsePaymentOptionDetail>(qrResponse.Content);
            if (qrResponseDto?.respCode == null)
            {
                throw new Exception(qrResponseDto.respDesc);
            }
        }
        /*
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
        */
    }

    public class Response2C2P
    {
        public string type { get; set; }
        public string data { get; set; }
        public string channelCode { get; set; }
        public string respCode { get; set; }
        public string respDesc { get; set; }
        public string payload { get; set; }
    }
    public class ResponsePaymentOption
    {
        public string paymentToken { get; set; }
        public string respCode { get; set; }
        public string respDesc { get; set; }
        public MerchantDetails merchantDetails { get; set; }
        public TransactionDetails transactionDetails { get; set; }
        public List<ChannelCategories> channelCategories { get; set; } 

    }
    public class MerchantDetails
    {
        public string id { get; set; }
        public string name { get; set; }
        public string address { get; set; }
        public string email { get; set; }
        public string logoUrl { get; set; }
        public string bannerUrl { get; set; }
    }
    public class TransactionDetails
    {
        public double amount { get; set; }
        public string currencyCode { get; set; }
        public string invoiceNo { get; set; }
        public string description { get; set; }
        public bool showFxRate { get; set; }
    }
    public class ChannelCategories
    {
        public int sequenceNo { get; set; }
        public string name { get; set; }
        public string code { get; set; }
        public string iconUrl { get; set; }
        public string logoUrl { get; set; }
        public bool expiration { get; set; }
        public List<Group> groups { get; set; }
    }
    public class Group
    {
        public int sequenceNo { get; set; }
        public string name { get; set; }
        public string code { get; set; }
        public string iconUrl { get; set; }
        public string logoUrl { get; set; }
        public bool expiration { get; set; }
    }

    public class ResponsePaymentOptionDetail
    {
        public int totalChannel { get; set; }
        public string name { get; set; }
        public string categoryCode { get; set; }
        public string groupCode { get; set; }
        public string iconUrl { get; set; }
        public string respCode { get; set; }
        public string respDesc { get; set; }
        public List<Channel> channels { get; set; }
        public Validation validation { get; set; }
        public Configuration configuration { get; set; }
    }
    public class Channel
    {
        public int sequenceNo { get; set; }
        public string name { get; set; }

    }
    public class Validation
    {
        
    }
    public class Configuration
    {

    }
}