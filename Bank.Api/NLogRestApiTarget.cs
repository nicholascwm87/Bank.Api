using NLog;
using NLog.Common;
using NLog.Config;
using NLog.Targets;

using System.ComponentModel;
using System.Globalization;
using System.Net;
using System.Text;


namespace Bank.Api
{
    [Target("RestApi")]
    public sealed class NLogRestApiTarget : MethodCallTargetBase
    {
        /// <summary>
        /// dictionary that maps a concrete <see cref="HttpPostFormatterBase"/> implementation
        /// to a specific <see cref="WebServiceProtocol"/>-value.
        /// </summary>
        private static Dictionary<WebServiceProtocol, Func<NLogRestApiTarget, HttpPostFormatterBase>> _postFormatterFactories =
            new Dictionary<WebServiceProtocol, Func<NLogRestApiTarget, HttpPostFormatterBase>>()
            {
                { WebServiceProtocol.HttpPost, t => new HttpPostFormEncodedFormatter(t)},
                { WebServiceProtocol.JsonPost, t => new HttpPostJsonFormatter(t)},
            };

        /// <summary>
        /// Initializes a new instance of the <see cref="NLogRestApiTarget" /> class.
        /// </summary>
        public NLogRestApiTarget()
        {
            this.Protocol = WebServiceProtocol.Soap11;

            //default NO utf-8 bom 
            this.Encoding = new UTF8Encoding(false);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NLogRestApiTarget" /> class.
        /// </summary>
        /// <param name="name">Name of the target</param>
        public NLogRestApiTarget(string name) : this()
        {
            this.Name = name;
        }

        /// <summary>
        /// Name of parameter that will be used in Http Authorization header. The parameter will also be excluded in payload body or query string.
        /// </summary>
        public string AuthorizationParameter { get; set; }

        public IList<MethodCallParameter> ContentParameters
        {
            get
            {
                return this.Parameters.Where(x => x.Name != AuthorizationParameter).ToList();
            }
        }

        /// <summary>
        /// Gets or sets the web service URL.
        /// </summary>
        /// <docgen category='Web Service Options' order='10' />
        public Uri Url { get; set; }

        /// <summary>
        /// Gets or sets the Web service method name. Only used with Soap.
        /// </summary>
        /// <docgen category='Web Service Options' order='10' />
        public string MethodName { get; set; }

        /// <summary>
        /// Gets or sets the Web service namespace. Only used with Soap.
        /// </summary>
        /// <docgen category='Web Service Options' order='10' />
        public string Namespace { get; set; }

        /// <summary>
        /// Gets or sets the protocol to be used when calling web service.
        /// </summary>
        /// <docgen category='Web Service Options' order='10' />
        [DefaultValue("JsonPost")]
        public WebServiceProtocol Protocol { get { return _activeProtocol.Key; } set { _activeProtocol = new KeyValuePair<WebServiceProtocol, HttpPostFormatterBase>(value, null); } }
        private KeyValuePair<WebServiceProtocol, HttpPostFormatterBase> _activeProtocol = new KeyValuePair<WebServiceProtocol, HttpPostFormatterBase>();

        /// <summary>
        /// Gets or sets the encoding.
        /// </summary>
        /// <docgen category='Web Service Options' order='10' />
        public Encoding Encoding { get; set; }

        /// <summary>
        /// Gets or sets a value whether escaping be done according to Rfc3986 (Supports Internationalized Resource Identifiers - IRIs)
        /// </summary>
        /// <value>A value of <c>true</c> if Rfc3986; otherwise, <c>false</c> for legacy Rfc2396.</value>
        /// <docgen category='Web Service Options' order='10' />
        public bool EscapeDataRfc3986 { get; set; }

        /// <summary>
        /// Gets or sets a value whether escaping be done according to the old NLog style (Very non-standard)
        /// </summary>
        /// <value>A value of <c>true</c> if legacy encoding; otherwise, <c>false</c> for standard UTF8 encoding.</value>
        /// <docgen category='Web Service Options' order='10' />
        public bool EscapeDataNLogLegacy { get; set; }

        private readonly AsyncOperationCounter pendingManualFlushList = new AsyncOperationCounter();

        /// <summary>
        /// Calls the target method. Must be implemented in concrete classes.
        /// </summary>
        /// <param name="parameters">Method call parameters.</param>
        protected override void DoInvoke(object[] parameters)
        {
            // method is not used, instead asynchronous overload will be used
            throw new NotImplementedException();
        }

        /// <summary>
        /// Invokes the web service method.
        /// </summary>
        /// <param name="parameters">Parameters to be passed.</param>
        /// <param name="continuation">The continuation.</param>
        protected override void DoInvoke(object[] parameters, AsyncContinuation continuation)
        {
            string authParamName = AuthorizationParameter;
            string authValue = null;

            if (!string.IsNullOrEmpty(authParamName) && !string.IsNullOrWhiteSpace(authParamName))
            {
                int index = this.Parameters.ToList().FindIndex(x => x.Name == authParamName);
                authValue = parameters[index].ToString();
            }

            var request = (HttpWebRequest)WebRequest.Create(BuildWebServiceUrl(parameters));

            if (!string.IsNullOrEmpty(authValue) && !string.IsNullOrWhiteSpace(authValue))
            {
                request.Headers["Authorization"] = authValue;
            }

            Func<AsyncCallback, IAsyncResult> begin = (r) => request.BeginGetRequestStream(r, null);
            Func<IAsyncResult, Stream> getStream = request.EndGetRequestStream;

            DoInvoke(parameters, continuation, request, begin, getStream);
        }

        internal void DoInvoke(object[] parameters, AsyncContinuation continuation, HttpWebRequest request, Func<AsyncCallback, IAsyncResult> beginFunc,
            Func<IAsyncResult, Stream> getStreamFunc)
        {
            Stream postPayload = null;

            if (Protocol == WebServiceProtocol.HttpGet)
            {
                PrepareGetRequest(request);
            }
            else
            {
                if (_activeProtocol.Value == null)
                    _activeProtocol = new KeyValuePair<WebServiceProtocol, HttpPostFormatterBase>(this.Protocol, _postFormatterFactories[this.Protocol](this));
                postPayload = _activeProtocol.Value.PrepareRequest(request, parameters);
            }

            AsyncContinuation sendContinuation =
                ex =>
                {
                    if (ex != null)
                    {
                        DoInvokeCompleted(continuation, ex);
                        return;
                    }

                    try
                    {
                        request.BeginGetResponse(
                            r =>
                            {
                                try
                                {
                                    using (var response = request.EndGetResponse(r))
                                    {
                                    }

                                    DoInvokeCompleted(continuation, null);
                                }
                                catch (Exception ex2)
                                {
                                    InternalLogger.Error(ex2, "Error when sending to Webservice: {0}", this.Name);
                                    if (ex2.MustBeRethrown())
                                    {
                                        throw;
                                    }

                                    DoInvokeCompleted(continuation, ex2);
                                }
                            },
                            null);
                    }
                    catch (Exception ex2)
                    {
                        InternalLogger.Error(ex2, "Error when sending to Webservice: {0}", this.Name);
                        if (ex2.MustBeRethrown())
                        {
                            throw;
                        }

                        DoInvokeCompleted(continuation, ex2);
                    }
                };

            if (postPayload != null && postPayload.Length > 0)
            {
                postPayload.Position = 0;
                try
                {
                    pendingManualFlushList.BeginOperation();

                    beginFunc(
                        result =>
                        {
                            try
                            {
                                using (Stream stream = getStreamFunc(result))
                                {
                                    WriteStreamAndFixPreamble(postPayload, stream, false, this.Encoding);

                                    postPayload.Dispose();
                                }

                                sendContinuation(null);
                            }
                            catch (Exception ex)
                            {
                                InternalLogger.Error(ex, "Error when sending to Webservice: {0}", this.Name);
                                if (ex.MustBeRethrown())
                                {
                                    throw;
                                }

                                postPayload.Dispose();
                                DoInvokeCompleted(continuation, ex);
                            }
                        });
                }
                catch (Exception ex)
                {
                    InternalLogger.Error(ex, "Error when sending to Webservice: {0}", this.Name);
                    if (ex.MustBeRethrown())
                    {
                        throw;
                    }

                    DoInvokeCompleted(continuation, ex);
                }
            }
            else
            {
                pendingManualFlushList.BeginOperation();
                sendContinuation(null);
            }
        }

        private void DoInvokeCompleted(AsyncContinuation continuation, Exception ex)
        {
            pendingManualFlushList.CompleteOperation(ex);
            continuation(ex);
        }

        /// <summary>
        /// Flush any pending log messages asynchronously (in case of asynchronous targets).
        /// </summary>
        /// <param name="asyncContinuation">The asynchronous continuation.</param>
        protected override void FlushAsync(AsyncContinuation asyncContinuation)
        {
            pendingManualFlushList.RegisterCompletionNotification(asyncContinuation).Invoke(null);
        }

        /// <summary>
        /// Closes the target.
        /// </summary>
        protected override void CloseTarget()
        {
            pendingManualFlushList.Clear();   // Maybe consider to wait a short while if pending requests?
            base.CloseTarget();
        }

        /// <summary>
        /// Builds the URL to use when calling the web service for a message, depending on the WebServiceProtocol.
        /// </summary>
        /// <param name="parameterValues"></param>
        /// <returns></returns>
        private Uri BuildWebServiceUrl(object[] parameterValues)
        {
            if (this.Protocol != WebServiceProtocol.HttpGet)
            {
                return this.Url;
            }

            UrlHelper.EscapeEncodingFlag encodingFlags = UrlHelper.GetUriStringEncodingFlags(EscapeDataNLogLegacy, false, EscapeDataRfc3986);

            //if the protocol is HttpGet, we need to add the parameters to the query string of the url
            var queryParameters = new StringBuilder();
            string separator = string.Empty;
            for (int i = 0; i < this.ContentParameters.Count; i++)
            {
                queryParameters.Append(separator);
                queryParameters.Append(this.Parameters[i].Name);
                queryParameters.Append("=");
                string parameterValue = Convert.ToString(parameterValues[i], CultureInfo.InvariantCulture);
                UrlHelper.EscapeDataEncode(parameterValue, queryParameters, encodingFlags);
                separator = "&";
            }

            var builder = new UriBuilder(this.Url);
            //append our query string to the URL following 
            //the recommendations at https://msdn.microsoft.com/en-us/library/system.uribuilder.query.aspx
            if (builder.Query != null && builder.Query.Length > 1)
            {
                builder.Query = string.Concat(builder.Query.Substring(1), "&", queryParameters.ToString());
            }
            else
            {
                builder.Query = queryParameters.ToString();
            }

            return builder.Uri;
        }

        private void PrepareGetRequest(HttpWebRequest request)
        {
            request.Method = "GET";
        }

        /// <summary>
        /// Write from input to output. Fix the UTF-8 bom
        /// </summary>
        /// <param name="input"></param>
        /// <param name="output"></param>
        /// <param name="writeUtf8BOM"></param>
        /// <param name="encoding"></param>
        private static void WriteStreamAndFixPreamble(Stream input, Stream output, bool? writeUtf8BOM, Encoding encoding)
        {
            //only when utf-8 encoding is used, the Encoding preamble is optional
            var nothingToDo = writeUtf8BOM == null || !(encoding is UTF8Encoding);

            const int preambleSize = 3;
            if (!nothingToDo)
            {
                //it's UTF-8
                var hasBomInEncoding = encoding.GetPreamble().Length == preambleSize;

                //BOM already in Encoding.
                nothingToDo = writeUtf8BOM.Value && hasBomInEncoding;

                //Bom already not in Encoding
                nothingToDo = nothingToDo || !writeUtf8BOM.Value && !hasBomInEncoding;
            }
            var offset = nothingToDo ? 0 : preambleSize;
            input.Position += offset;
            input.CopyTo(output);
            //input.CopyWithOffset(output, offset);

        }

        /// <summary>
        /// base class for POST formatters, that
        /// implement former <c>PrepareRequest()</c> method,
        /// that creates the content for
        /// the requested kind of HTTP request
        /// </summary>
        private abstract class HttpPostFormatterBase
        {
            protected HttpPostFormatterBase(NLogRestApiTarget target)
            {
                Target = target;
            }

            protected abstract string ContentType { get; }
            protected NLogRestApiTarget Target { get; private set; }

            public MemoryStream PrepareRequest(HttpWebRequest request, object[] parameterValues)
            {
                InitRequest(request);

                var ms = new MemoryStream();
                WriteContent(ms, parameterValues);
                return ms;
            }

            protected virtual void InitRequest(HttpWebRequest request)
            {
                request.Method = "POST";
                request.ContentType = string.Concat(ContentType, "; charset=", Target.Encoding.WebName);
            }

            protected abstract void WriteContent(MemoryStream ms, object[] parameterValues);
        }

        private class HttpPostFormEncodedFormatter : HttpPostTextFormatterBase
        {
            readonly UrlHelper.EscapeEncodingFlag encodingFlags;

            public HttpPostFormEncodedFormatter(NLogRestApiTarget target) : base(target)
            {
                encodingFlags = UrlHelper.GetUriStringEncodingFlags(target.EscapeDataNLogLegacy, true, target.EscapeDataRfc3986);
            }

            protected override string ContentType
            {
                get { return "application/x-www-form-urlencoded"; }
            }

            protected override string Separator
            {
                get { return "&"; }
            }

            protected override string GetFormattedContent(string parametersContent)
            {
                return parametersContent;
            }

            protected override string GetFormattedParameter(MethodCallParameter parameter, object value)
            {
                string parameterValue = Convert.ToString(value, CultureInfo.InvariantCulture);
                if (string.IsNullOrEmpty(parameterValue))
                {
                    return string.Concat(parameter.Name, "=");
                }

                var sb = new StringBuilder(parameter.Name.Length + parameterValue.Length + 20);
                sb.Append(parameter.Name).Append("=");
                UrlHelper.EscapeDataEncode(parameterValue, sb, encodingFlags);
                return sb.ToString();
            }
        }

        private class HttpPostJsonFormatter : HttpPostTextFormatterBase
        {
            public HttpPostJsonFormatter(NLogRestApiTarget target) : base(target)
            { }

            protected override string ContentType
            {
                get { return "application/json"; }
            }

            protected override string Separator
            {
                get { return ","; }
            }

            protected override string GetFormattedContent(string parametersContent)
            {
                return string.Concat("{", parametersContent, "}");
            }

            protected override string GetFormattedParameter(MethodCallParameter parameter, object value)
            {
                return string.Concat("\"", parameter.Name, "\":", GetJsonValueString(value));
            }

            private string GetJsonValueString(object value)
            {
                //return ConfigurationItemFactory.Default.JsonSerializer.SerializeObject(value);
                var retVal = string.Empty;
                var builder = new StringBuilder();
                if (ConfigurationItemFactory.Default.JsonConverter.SerializeObject(value, builder))
                {
                    retVal = builder.ToString();
                }
                return retVal;
            }
        }

        private abstract class HttpPostTextFormatterBase : HttpPostFormatterBase
        {
            protected HttpPostTextFormatterBase(NLogRestApiTarget target) : base(target)
            {
            }

            protected abstract string Separator { get; }

            protected abstract string GetFormattedContent(string parametersContent);

            protected abstract string GetFormattedParameter(MethodCallParameter parameter, object value);

            protected override void WriteContent(MemoryStream ms, object[] parameterValues)
            {
                var sw = new StreamWriter(ms, Target.Encoding);
                sw.Write(string.Empty);

                var sb = new StringBuilder();
                for (int i = 0; i < Target.ContentParameters.Count; i++)
                {
                    if (sb.Length > 0) sb.Append(Separator);
                    sb.Append(GetFormattedParameter(Target.ContentParameters[i], parameterValues[i]));
                }
                string content = GetFormattedContent(sb.ToString());
                sw.Write(content);
                sw.Flush();
            }
        }
    }

    /// <summary>
    /// Keeps track of pending operation count, and can notify when pending operation count reaches zero
    /// </summary>
    internal class AsyncOperationCounter
    {
        private int _pendingOperationCounter;
        private readonly LinkedList<AsyncContinuation> _pendingCompletionList = new LinkedList<AsyncContinuation>();

        /// <summary>
        /// Mark operation has started
        /// </summary>
        public void BeginOperation()
        {
            System.Threading.Interlocked.Increment(ref _pendingOperationCounter);
        }

        /// <summary>
        /// Mark operation has completed
        /// </summary>
        /// <param name="exception">Exception coming from the completed operation [optional]</param>
        public void CompleteOperation(Exception exception)
        {
            if (_pendingCompletionList.Count > 0)
            {
                lock (_pendingCompletionList)
                {
                    System.Threading.Interlocked.Decrement(ref _pendingOperationCounter);
                    var nodeNext = _pendingCompletionList.First;
                    while (nodeNext != null)
                    {
                        var nodeValue = nodeNext.Value;
                        nodeNext = nodeNext.Next;
                        nodeValue(exception);  // Will modify _pendingCompletionList
                    }
                }
            }
            else
            {
                System.Threading.Interlocked.Decrement(ref _pendingOperationCounter);
            }
        }

        /// <summary>
        /// Registers an AsyncContinuation to be called when all pending operations have completed
        /// </summary>
        /// <param name="asyncContinuation">Invoked on completion</param>
        /// <returns>AsyncContinuation operation</returns>
        public AsyncContinuation RegisterCompletionNotification(AsyncContinuation asyncContinuation)
        {
            if (_pendingOperationCounter == 0)
            {
                return asyncContinuation;
            }
            else
            {
                lock (_pendingCompletionList)
                {
                    var pendingCompletion = new LinkedListNode<AsyncContinuation>(null);
                    _pendingCompletionList.AddLast(pendingCompletion);

                    // We only want to wait for the operations currently in progress (not the future operations)
                    int remainingCompletionCounter = System.Threading.Interlocked.Increment(ref _pendingOperationCounter);
                    if (remainingCompletionCounter <= 0)
                    {
                        System.Threading.Interlocked.Exchange(ref _pendingOperationCounter, 0);
                        _pendingCompletionList.Remove(pendingCompletion);
                        return asyncContinuation;
                    }

                    pendingCompletion.Value = (ex) =>
                    {
                        if (System.Threading.Interlocked.Decrement(ref remainingCompletionCounter) == 0)
                        {
                            lock (_pendingCompletionList)
                            {
                                System.Threading.Interlocked.Decrement(ref _pendingOperationCounter);
                                _pendingCompletionList.Remove(pendingCompletion);
                                var nodeNext = _pendingCompletionList.First;
                                while (nodeNext != null)
                                {
                                    var nodeValue = nodeNext.Value;
                                    nodeNext = nodeNext.Next;
                                    nodeValue(ex);  // Will modify _pendingCompletionList
                                }
                            }
                            asyncContinuation(ex);
                        }
                    };

                    return pendingCompletion.Value;
                }
            }
        }

        /// <summary>
        /// Clear o
        /// </summary>
        public void Clear()
        {
            _pendingCompletionList.Clear();
        }
    }

    /// <summary>
    /// URL Encoding helper.
    /// </summary>
    internal static class UrlHelper
    {
        [Flags]
        public enum EscapeEncodingFlag
        {
            None = 0,
            /// <summary>Allow UnreservedMarks instead of ReservedMarks, as specified by chosen RFC</summary>
            UriString = 1,
            /// <summary>Use RFC2396 standard (instead of RFC3986)</summary>
            LegacyRfc2396 = 2,
            /// <summary>Should use lowercase when doing HEX escaping of special characters</summary>
            LowerCaseHex = 4,
            /// <summary>Replace space ' ' with '+' instead of '%20'</summary>
            SpaceAsPlus = 8,
            /// <summary>Skip UTF8 encoding, and prefix special characters with '%u'</summary>
            NLogLegacy = 16 | LegacyRfc2396 | LowerCaseHex | UriString,
        };

        /// <summary>
        /// Escape unicode string data for use in http-requests
        /// </summary>
        /// <param name="source">unicode string-data to be encoded</param>
        /// <param name="target">target for the encoded result</param>
        /// <param name="flags"><see cref="EscapeEncodingFlag"/>s for how to perform the encoding</param>
        public static void EscapeDataEncode(string source, StringBuilder target, EscapeEncodingFlag flags)
        {
            if (string.IsNullOrEmpty(source))
                return;

            bool isUriString = (flags & EscapeEncodingFlag.UriString) == EscapeEncodingFlag.UriString;
            bool isLegacyRfc2396 = (flags & EscapeEncodingFlag.LegacyRfc2396) == EscapeEncodingFlag.LegacyRfc2396;
            bool isLowerCaseHex = (flags & EscapeEncodingFlag.LowerCaseHex) == EscapeEncodingFlag.LowerCaseHex;
            bool isSpaceAsPlus = (flags & EscapeEncodingFlag.SpaceAsPlus) == EscapeEncodingFlag.SpaceAsPlus;
            bool isNLogLegacy = (flags & EscapeEncodingFlag.NLogLegacy) == EscapeEncodingFlag.NLogLegacy;

            char[] charArray = null;
            byte[] byteArray = null;
            char[] hexChars = isLowerCaseHex ? hexLowerChars : hexUpperChars;

            for (int i = 0; i < source.Length; ++i)
            {
                char ch = source[i];
                target.Append(ch);
                if (ch >= 'a' && ch <= 'z')
                    continue;
                if (ch >= 'A' && ch <= 'Z')
                    continue;
                if (ch >= '0' && ch <= '9')
                    continue;
                if (isSpaceAsPlus && ch == ' ')
                {
                    target[target.Length - 1] = '+';
                    continue;
                }

                if (isUriString)
                {
                    if (!isLegacyRfc2396 && RFC3986UnreservedMarks.IndexOf(ch) >= 0)
                        continue;
                    if (isLegacyRfc2396 && RFC2396UnreservedMarks.IndexOf(ch) >= 0)
                        continue;
                }
                else
                {
                    if (!isLegacyRfc2396 && RFC3986ReservedMarks.IndexOf(ch) >= 0)
                        continue;
                    if (isLegacyRfc2396 && RFC2396ReservedMarks.IndexOf(ch) >= 0)
                        continue;
                }

                if (isNLogLegacy)
                {
                    if (ch < 256)
                    {
                        target[target.Length - 1] = '%';
                        target.Append(hexChars[(ch >> 4) & 0xF]);
                        target.Append(hexChars[(ch >> 0) & 0xF]);
                    }
                    else
                    {
                        target[target.Length - 1] = '%';
                        target.Append('u');
                        target.Append(hexChars[(ch >> 12) & 0xF]);
                        target.Append(hexChars[(ch >> 8) & 0xF]);
                        target.Append(hexChars[(ch >> 4) & 0xF]);
                        target.Append(hexChars[(ch >> 0) & 0xF]);
                    }
                    continue;
                }

                if (charArray == null)
                    charArray = new char[1];
                charArray[0] = ch;

                if (byteArray == null)
                    byteArray = new byte[8];

                // Convert the wide-char into utf8-bytes, and then escape
                int byteCount = Encoding.UTF8.GetBytes(charArray, 0, 1, byteArray, 0);
                for (int j = 0; j < byteCount; ++j)
                {
                    byte byteCh = byteArray[j];
                    if (j == 0)
                        target[target.Length - 1] = '%';
                    else
                        target.Append('%');
                    target.Append(hexChars[(byteCh & 0xf0) >> 4]);
                    target.Append(hexChars[byteCh & 0xf]);
                }
            }
        }

        private const string RFC2396ReservedMarks = @";/?:@&=+$,";
        private const string RFC3986ReservedMarks = @":/?#[]@!$&'()*+,;=";
        private const string RFC2396UnreservedMarks = @"-_.!~*'()";
        private const string RFC3986UnreservedMarks = @"-._~";

        private static readonly char[] hexUpperChars =
            { '0', '1', '2', '3', '4', '5', '6', '7',
            '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };
        private static readonly char[] hexLowerChars =
            { '0', '1', '2', '3', '4', '5', '6', '7',
            '8', '9', 'a', 'b', 'c', 'd', 'e', 'f' };

        public static EscapeEncodingFlag GetUriStringEncodingFlags(bool escapeDataNLogLegacy, bool spaceAsPlus, bool escapeDataRfc3986)
        {
            EscapeEncodingFlag encodingFlags = EscapeEncodingFlag.UriString;
            if (escapeDataNLogLegacy)
                encodingFlags |= EscapeEncodingFlag.LowerCaseHex | EscapeEncodingFlag.NLogLegacy;
            else if (!escapeDataRfc3986)
                encodingFlags |= EscapeEncodingFlag.LowerCaseHex | EscapeEncodingFlag.LegacyRfc2396;
            if (spaceAsPlus)
                encodingFlags |= EscapeEncodingFlag.SpaceAsPlus;
            return encodingFlags;
        }
    }

    /// <summary>
    /// Helper class for dealing with exceptions.
    /// </summary>
    internal static class ExceptionHelper
    {
        private const string LoggedKey = "NLog.ExceptionLoggedToInternalLogger";

        /// <summary>
        /// Mark this exception as logged to the <see cref="InternalLogger"/>.
        /// </summary>
        /// <param name="exception"></param>
        /// <returns></returns>
        public static void MarkAsLoggedToInternalLogger(this Exception exception)
        {
            if (exception != null)
            {
                exception.Data[LoggedKey] = true;
            }
        }

        /// <summary>
        /// Is this exception logged to the <see cref="InternalLogger"/>? 
        /// </summary>
        /// <param name="exception"></param>
        /// <returns><c>true</c>if the <paramref name="exception"/> has been logged to the <see cref="InternalLogger"/>.</returns>
        public static bool IsLoggedToInternalLogger(this Exception exception)
        {
            if (exception != null)
            {
                return exception.Data[LoggedKey] as bool? ?? false;
            }
            return false;
        }


        /// <summary>
        /// Determines whether the exception must be rethrown and logs the error to the <see cref="InternalLogger"/> if <see cref="IsLoggedToInternalLogger"/> is <c>false</c>.
        /// 
        /// Advised to log first the error to the <see cref="InternalLogger"/> before calling this method.
        /// </summary>
        /// <param name="exception">The exception to check.</param>
        /// <returns><c>true</c>if the <paramref name="exception"/> must be rethrown, <c>false</c> otherwise.</returns>
        public static bool MustBeRethrown(this Exception exception)
        {
            if (exception.MustBeRethrownImmediately())
            {
                //no futher logging, because it can make servere exceptions only worse.
                return true;
            }

            var isConfigError = exception is NLogConfigurationException;

            //we throw always configuration exceptions (historical)
            if (!exception.IsLoggedToInternalLogger())
            {
                var level = isConfigError ? NLog.LogLevel.Warn : NLog.LogLevel.Error;
                InternalLogger.Log(exception, level, "Error has been raised.");
            }

            //if ThrowConfigExceptions == null, use  ThrowExceptions
            var shallRethrow = isConfigError ? (LogManager.ThrowConfigExceptions ?? LogManager.ThrowExceptions) : LogManager.ThrowExceptions;
            return shallRethrow;
        }

        /// <summary>
        /// Determines whether the exception must be rethrown immediately, without logging the error to the <see cref="InternalLogger"/>.
        /// 
        /// Only used this method in special cases.
        /// </summary>
        /// <param name="exception">The exception to check.</param>
        /// <returns><c>true</c>if the <paramref name="exception"/> must be rethrown, <c>false</c> otherwise.</returns>
        public static bool MustBeRethrownImmediately(this Exception exception)
        {
            // Use this for non core
            /*
            if (exception is StackOverflowException)
            {
                return true;
            }

            if (exception is ThreadAbortException)
            {
                return true;
            }
            */

            if (exception is OutOfMemoryException)
            {
                return true;
            }

            return false;
        }
    }
}
