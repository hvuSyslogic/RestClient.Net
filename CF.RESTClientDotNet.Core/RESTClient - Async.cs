using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace CF.RESTClientDotNet
{
    public partial class RESTClient
    {
        #region Constructor

        public RESTClient(ISerializationAdapter serializationAdapter, Uri baseUri)
        {
            SerializationAdapter = serializationAdapter;
            BaseUri = baseUri;
        }

        #endregion

        #region Public Methods

        #region POST
        /// <summary>
        /// Make REST POST call and wait for the response
        /// </summary>
        public async Task<RESTResponse<ReturnT>> PostAsync<ReturnT>()
        {
            return await CallAsync<ReturnT, object, object>(BaseUri, null, null, HttpVerb.Post, Headers, TimeoutMilliseconds, ReadToEnd);
        }

        /// <summary>
        /// Make REST POST call and wait for the response
        /// </summary>
        public async Task<RESTResponse<ReturnT>> PostAsync<ReturnT, BodyT>(BodyT body)
        {
            return await CallAsync<ReturnT, BodyT, object>(BaseUri, body, null, HttpVerb.Post, Headers, TimeoutMilliseconds, ReadToEnd);
        }

        public async Task<RESTResponse> PostAsync<BodyT, QueryStringT>(BodyT body, QueryStringT queryString)
        {
            return await CallAsync<object, BodyT, object>(BaseUri, body, queryString, HttpVerb.Post, Headers, TimeoutMilliseconds, ReadToEnd);
        }

        #endregion

        #region PUT
        /// <summary>
        /// Make REST PUT call and wait for the response
        /// </summary>
        public async Task<RESTResponse<ReturnT>> PutAsync<ReturnT, BodyT, QueryStringT>(BodyT body, QueryStringT queryString)
        {
            //TODO: This method currently remains untested. But can be tested by uncommenting this line.");
            return await CallAsync<ReturnT, BodyT, QueryStringT>(BaseUri, body, queryString, HttpVerb.Put, Headers, TimeoutMilliseconds, ReadToEnd);
        }
        #endregion

        #region GET
        /// <summary>
        /// Make a GET call and wait for the response
        /// </summary>
        public async Task<RESTResponse<ReturnT>> GetAsync<ReturnT, QueryStringT>(QueryStringT queryString)
        {
            return await CallAsync<ReturnT, object, QueryStringT>(BaseUri, null, queryString, HttpVerb.Get, Headers, TimeoutMilliseconds, ReadToEnd);
        }

        /// <summary>
        /// Make a GET call and wait for the response
        /// </summary>
        /// 
        public async Task<RESTResponse<ReturnT>> GetAsync<ReturnT>()
        {
            return await CallAsync<ReturnT, object, object>(BaseUri, null, null, HttpVerb.Get, Headers, TimeoutMilliseconds, ReadToEnd);
        }

        public async Task<RESTResponse> GetAsync()
        {
            return await GetRESTResponse(BaseUri, null, null, HttpVerb.Get, Headers, TimeoutMilliseconds, ReadToEnd);
        }
        #endregion

        #endregion

        #region Private Methods

        #region Base Calls

        /// <summary>
        /// Make REST call and wait for the response
        /// </summary>
        private static async Task<WebResponse> GetWebResponse(Uri baseUri, object body, object queryString, HttpVerb verb, Dictionary<string, string> headers, int timeOutMilliseconds, bool readToEnd)
        {
            try
            {
                //Get the Http Request object
                var request = await GetRequestAsync(baseUri, body, queryString, verb, headers, timeOutMilliseconds);

                //Make the call to the server and wait for the response
                var response = await request.GetResponseAsync();

                //Return the response
                return response;
            }
            catch (WebException wex)
            {
                try
                {
                    using (var streamReader = new StreamReader(wex.Response.GetResponseStream()))
                    {
                        var responseText = GetDataFromResponseStreamAsync(wex.Response, readToEnd);

                        //var retVal = await DeserialiseResponseAsync<T>(restResponse);
                    }
                }
                catch(Exception ex)
                {
                    throw new Exception("Error handling Error from web server." + ex.Message, ex);
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("The request timed out"))
                {
                    //The REST call timed out so throw this exception
                    throw new RESTTimeoutException(timeOutMilliseconds / 1000);
                }
                else
                {
                    throw;
                }
            }
        }

        private static async Task<RESTResponse<T>> CallAsync<T, T1, T2>(Uri baseUri, T1 body, T2 queryString, HttpVerb verb, Dictionary<string, string> headers, int timeOutMilliseconds, bool readToEnd)
        {
            var restResponse = await GetRESTResponse(baseUri, body, queryString, verb, headers, timeOutMilliseconds, readToEnd);

            var retVal = await DeserialiseResponseAsync<T>(restResponse);

            return retVal;
        }

        private static async Task<RESTResponse> GetRESTResponse(Uri baseUri, object body, object queryString, HttpVerb verb, Dictionary<string, string> headers, int timeOutMilliseconds, bool readToEnd)
        {
            var webResponse = await GetWebResponse(baseUri, body, queryString, verb, headers, timeOutMilliseconds, readToEnd);

            var restResponse = new RESTResponse();
            restResponse.Response = webResponse;
            restResponse.Data = await GetDataFromResponseStreamAsync(webResponse, readToEnd);
            return restResponse;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Creates a HttpWebRequest so that the REST call can be made with it
        /// </summary>
        private static async Task<WebRequest> GetRequestAsync<ReturnT, QueryStringT>(Uri baseUri, ReturnT body, QueryStringT queryString, HttpVerb verb, Dictionary<string, string> headers, int timeOutMilliseconds)
        {
            var theUri = baseUri;

            if (queryString != null)
            {
                string queryStringText;
                if (PrimitiveTypes.Contains(queryString.GetType()))
                {
                    //No need to serialize
                    queryStringText = queryString.ToString();
                }
                else
                {
                    queryStringText = await SerializationAdapter.SerializeAsync<ReturnT>(queryString);
                    queryStringText = Uri.EscapeDataString(queryStringText);
                }

                theUri = new Uri($"{ theUri.AbsoluteUri}/{queryStringText}");
            }

            //Create the web request
            var retVal = (HttpWebRequest)WebRequest.Create(theUri);

            //Switch on the verb
            switch (verb)
            {
                case HttpVerb.Post:
                    retVal.Method = "POST";
                    break;
                case HttpVerb.Get:
                    retVal.Method = "GET";
                    break;
                case HttpVerb.Put:
                    retVal.Method = "PUT";
                    break;
                default:
                    throw new NotImplementedException();
            }

            //We're always going to use json
            retVal.ContentType = "application/json";

            if (body != null && new List<HttpVerb> { HttpVerb.Post, HttpVerb.Put }.Contains(verb))
            {
                //Set the body of the POST/PUT

                //Serialised JSon data
                var markup = await SerializationAdapter.SerializeAsync<ReturnT>(body);

                //Get the json as a byte array
                var markupBuffer = await SerializationAdapter.DecodeStringAsync(markup);

                using (var requestStream = await retVal.GetRequestStreamAsync())
                {
                    requestStream.Write(markupBuffer, 0, markupBuffer.Length);
                }
            }

            foreach (var key in headers?.Keys)
            {
                retVal.Headers[key] = headers[key];
            }

            //TODO: Reimplement
            //#if (!NETFX_CORE && !SILVERLIGHT)
            //            retVal.Timeout = timeOutMilliseconds;
            //#endif

            //Return the request
            return retVal;
        }



        /// <summary>
        /// Given the response from the REST call, return the string(
        /// </summary>
        private static async Task<string> GetDataFromResponseStreamAsync(WebResponse response, bool readToEnd)
        {
            var responseStream = response.GetResponseStream();
            byte[] responseBuffer = null;

            if (!readToEnd)
            {
                if (responseStream.Length == -1)
                {
                    throw new Exception("An error occurred while getting data from the server. Please contact support");
                }

                //Read the stream in to a buffer
                responseBuffer = new byte[responseStream.Length];

                //Read from the stream (complete)
                var responseLength = await responseStream.ReadAsync(responseBuffer, 0, (int)responseStream.Length);
            }
            else
            {
                var reader = new StreamReader(responseStream);
                return await reader.ReadToEndAsync();
            }

            //Convert the response from bytes to json string 
            return await SerializationAdapter.EncodeStringAsync(responseBuffer);
        }

        /// <summary>
        /// Turn a non-generic RESTResponse in to a generic one. 
        /// </summary>
        private static async Task<RESTResponse<T>> DeserialiseResponseAsync<T>(RESTResponse response)
        {
            var retVal = new RESTResponse<T>();

            if (typeof(T) == typeof(string))
            {
                retVal.Data = (T)(object)response.Data;
            }
            else
            {
                //Deserialise the json to the generic type
                retVal.Data = await SerializationAdapter.DeserializeAsync<T>(response.Data);
            }

            //Set the HttpWebResponse
            retVal.Response = response.Response;

            return retVal;
        }

        #endregion

        #endregion
    }
}