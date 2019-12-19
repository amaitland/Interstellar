﻿// Adapted from: https://stackoverflow.com/questions/41745827/cefsharp-inject-javascript-prior-to-any-document-load-processing
// TODO: translate this to F# to eliminate the need for a C# library, as well as for my own proper comprehension of this code... I hate having to
// directly copy-paste code from Stackoverflow without completely understanding it. That's bad news!
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CefSharp;
using CefSharp.Handler;

namespace CefSharp.JSInjectorResponseFilter
{
    public class JavascriptInjectionRequestHandler : RequestHandler
    {
        readonly string injectionPayload;

        public JavascriptInjectionRequestHandler(string injectionPayload) {
            this.injectionPayload = injectionPayload;
        }

        public IResponseFilter GetResourceResponseFilter(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, IResponse response)
        {
            if (frame.IsMain && request.ResourceType == ResourceType.MainFrame)
            {
                return new JavascriptInjectionFilter(injectionPayload);
            }
            return null;
        }
    }

    public class JavascriptInjectionFilter : IResponseFilter
    {
        /// <summary>
        /// Location to insert the javascript
        /// </summary>
        public enum Locations
        {
            /// <summary>
            /// Insert Javascript at the top of the header element
            /// </summary>
            head,
            /// <summary>
            /// Insert Javascript at the top of the body element
            /// </summary>
            body
        }

        string injection;
        string location;
        int offset = 0;
        List<byte> overflow = new List<byte>();

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="injection"></param>
        /// <param name="location"></param>
        public JavascriptInjectionFilter(string injection, Locations location = Locations.head)
        {
            this.injection = "<script>" + injection + "</script>";
            switch (location)
            {
                case Locations.head:
                    this.location = "<head>";
                    break;

                case Locations.body:
                    this.location = "<body>";
                    break;

                default:
                    this.location = "<head>";
                    break;
            }
        }

        /// <summary>
        /// Disposal
        /// </summary>
        public void Dispose()
        {
            //
        }

        /// <summary>
        /// Filter Processing...  handles the injection
        /// </summary>
        /// <param name="dataIn"></param>
        /// <param name="dataInRead"></param>
        /// <param name="dataOut"></param>
        /// <param name="dataOutWritten"></param>
        /// <returns></returns>
        public FilterStatus Filter(Stream dataIn, out long dataInRead, Stream dataOut, out long dataOutWritten)
        {
            dataInRead = dataIn == null ? 0 : dataIn.Length;
            dataOutWritten = 0;

            if (overflow.Count > 0)
            {
                var buffersize = Math.Min(overflow.Count, (int)dataOut.Length);
                dataOut.Write(overflow.ToArray(), 0, buffersize);
                dataOutWritten += buffersize;

                if (buffersize < overflow.Count)
                {
                    overflow.RemoveRange(0, buffersize - 1);
                }
                else
                {
                    overflow.Clear();
                }
            }


            for (var i = 0; i < dataInRead; ++i)
            {
                var readbyte = (byte)dataIn.ReadByte();
                var readchar = Convert.ToChar(readbyte);
                var buffersize = dataOut.Length - dataOutWritten;

                if (buffersize > 0)
                {
                    dataOut.WriteByte(readbyte);
                    dataOutWritten++;
                }
                else
                {
                    overflow.Add(readbyte);
                }

                if (char.ToLower(readchar) == location[offset])
                {
                    offset++;
                    if (offset >= location.Length)
                    {
                        offset = 0;
                        buffersize = Math.Min(injection.Length, dataOut.Length - dataOutWritten);

                        if (buffersize > 0)
                        {
                            var data = Encoding.UTF8.GetBytes(injection);
                            dataOut.Write(data, 0, (int)buffersize);
                            dataOutWritten += buffersize;
                        }

                        if (buffersize < injection.Length)
                        {
                            var remaining = injection.Substring((int)buffersize, (int)(injection.Length - buffersize));
                            overflow.AddRange(Encoding.UTF8.GetBytes(remaining));
                        }

                    }
                }
                else
                {
                    offset = 0;
                }

            }

            if (overflow.Count > 0 || offset > 0)
            {
                return FilterStatus.NeedMoreData;
            }

            return FilterStatus.Done;
        }

        /// <summary>
        /// Initialization
        /// </summary>
        /// <returns></returns>
        public bool InitFilter()
        {
            return true;
        }
    }
}
