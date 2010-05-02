// ============================================================================
// FileName: StatelessProxyScriptHelper.cs
//
// Description:
// A class that contains helper methods for use in a stateless prxoy runtime script.
//
// Author(s):
// Aaron Clauson
//
// License: 
// This software is licensed under the BSD License http://www.opensource.org/licenses/bsd-license.php
//
// Copyright (c) 2006-2009 Aaron Clauson (aaronc@blueface.ie), Blue Face Ltd, Dublin, Ireland (www.blueface.ie)
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without modification, are permitted provided that 
// the following conditions are met:
//
// Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer. 
// Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following 
// disclaimer in the documentation and/or other materials provided with the distribution. Neither the name of Blue Face Ltd. 
// nor the names of its contributors may be used to endorse or promote products derived from this software without specific 
// prior written permission. 
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, 
// BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. 
// IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, 
// OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, 
// OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, 
// OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE 
// POSSIBILITY OF SUCH DAMAGE.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using SIPSorcery.Net;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;
using SIPSorcery.Sys;
using log4net;

#if UNITTEST
using NUnit.Framework;
#endif

namespace SIPSorcery.Servers
{
    public class SIPProxyScriptFacade
    {
        private static ILog logger = log4net.LogManager.GetLogger("sipproxy");

        private SIPMonitorLogDelegate m_proxyLogger;
        private SIPTransport m_sipTransport;
        private SIPProxyDispatcher m_dispatcher;

        private GetAppServerDelegate GetAppServer_External;

        public SIPProxyScriptFacade(
            SIPMonitorLogDelegate proxyLogger,
            SIPTransport sipTransport,
            SIPProxyDispatcher dispatcher,
            GetAppServerDelegate getAppServer)
        {
            m_proxyLogger = proxyLogger;
            m_sipTransport = sipTransport;
            m_dispatcher = dispatcher;
            GetAppServer_External = getAppServer;
        }

        public void Log(string message)
        {
            m_proxyLogger(new SIPMonitorConsoleEvent(SIPMonitorServerTypesEnum.SIPProxy, SIPMonitorEventTypesEnum.DialPlan, message, null));
        }

        public void Send(string dstSocket, SIPRequest sipRequest, string proxyBranch)
        {
            SIPEndPoint dstSIPEndPoint = SIPEndPoint.ParseSIPEndPoint(dstSocket);
            Send(dstSIPEndPoint, sipRequest, proxyBranch, null, null);
        }

        public void Send(string dstSocket, SIPRequest sipRequest, string proxyBranch, string localSocket)
        {
            SIPEndPoint dstSIPEndPoint = SIPEndPoint.ParseSIPEndPoint(dstSocket);
            SIPEndPoint localSIPEndPoint = SIPEndPoint.ParseSIPEndPoint(localSocket);
            Send(dstSIPEndPoint, sipRequest, proxyBranch, localSIPEndPoint, null);
        }

        public void Send(SIPEndPoint dstSIPEndPoint, SIPRequest sipRequest, string proxyBranch, SIPEndPoint localSIPEndPoint)
        {
            Send(dstSIPEndPoint, sipRequest, proxyBranch, localSIPEndPoint, null);
        }

        /// <summary>
        /// Forwards a SIP request through the Proxy.
        /// </summary>
        /// <param name="dstSIPEndPoint">The destination SIP socket to send the request to.</param>
        /// <param name="sipRequest">The SIP request to send.</param>
        /// <param name="proxyBranch">The branchid to set on the Via header to be added for this forwarding leg.</param>
        /// <param name="localSIPEndPoint">The proxy socket that the request should be sent from. If null the 
        /// default channel that matches the destination end point should be used.</param>
        /// <param name="setContactToLocal">If true the Contact header URI will be overwritten with the URI of the
        /// proxy socket the request is being sent from. This should only be used for INVITE requests.</param>
        public void Send(SIPEndPoint dstSIPEndPoint, SIPRequest sipRequest, string proxyBranch, SIPEndPoint localSIPEndPoint, IPAddress publicIPAddress)
        {
            if (dstSIPEndPoint == null)
            {
                Log("Send was passed an emtpy destination for " + sipRequest.URI.ToString() + ", returning unresolvable.");
                Respond(sipRequest, SIPResponseStatusCodesEnum.NotFound, "DNS lookup unresolvable");
                return;
            }

            // Determine the external SIP endpoint that the proxy will use to send this request. If the ProxySendFrom header is set
            // it overrides any specified value.
            if (!sipRequest.Header.ProxySendFrom.IsNullOrBlank())
            {
                SIPChannel proxyChannel = m_sipTransport.FindSIPChannel(SIPEndPoint.ParseSIPEndPoint(sipRequest.Header.ProxySendFrom));
                localSIPEndPoint = (proxyChannel != null) ? proxyChannel.SIPChannelEndPoint : null;
            }
            localSIPEndPoint = localSIPEndPoint ?? m_sipTransport.GetDefaultSIPEndPoint(dstSIPEndPoint);

            // If the request is being forwarded on a different proxy socket to the one it was received on then two Via headers
            // need to be added, one for the proxy socket the request was received on and one for the socket it is being sent on.
            if (localSIPEndPoint != sipRequest.LocalSIPEndPoint)
            {
                SIPViaHeader receiveSocketVia = new SIPViaHeader(sipRequest.LocalSIPEndPoint, CallProperties.CreateBranchId());
                sipRequest.Header.Vias.PushViaHeader(receiveSocketVia);
            }

            SIPViaHeader via = new SIPViaHeader(localSIPEndPoint, proxyBranch);
            sipRequest.Header.Vias.PushViaHeader(via);

            sipRequest.LocalSIPEndPoint = localSIPEndPoint;

            //if (contactURI != null && sipRequest.Header.Contact != null && sipRequest.Header.Contact.Count > 0)
            //{
            //    sipRequest.Header.Contact[0].ContactURI = contactURI;
            //}

            if (sipRequest.Method != SIPMethodsEnum.REGISTER)
            {
                AdjustContactHeader(sipRequest, localSIPEndPoint, publicIPAddress);
            }

            m_sipTransport.SendRequest(dstSIPEndPoint, sipRequest);
        }

        /// <summary>
        /// Forwards a SIP request through the Proxy. This method differs from the standard Send in that irrespective of whether the Proxy is
        /// receiving and sending on different sockets only a single Via header will ever be allowed on the request. It is then up to the
        /// response processing logic to determine from which Proxy socket to forward the request and to add back on the Via header for the 
        /// end agent. This method is only ever used for requests destined for EXTERNAL SIP end points. 
        /// </summary>
        /// <param name="dstSIPEndPoint">The destination SIP socket to send the request to.</param>
        /// <param name="sipRequest">The SIP request to send.</param>
        /// <param name="proxyBranch">The branchid to set on the Via header to be added for this forwarding leg.</param>
        /// default channel that matches the destination end point should be used.</param>
        public void SendTransparent(SIPEndPoint dstSIPEndPoint, SIPRequest sipRequest, string proxyBranch, IPAddress publicIPAddress)
        {
            if (dstSIPEndPoint == null)
            {
                Log("Send was passed an emtpy destination for " + sipRequest.URI.ToString() + ", returning unresolvable.");
                Respond(sipRequest, SIPResponseStatusCodesEnum.NotFound, "DNS lookup unresolvable");
                return;
            }

            // Determine the external SIP endpoint that the proxy will use to send this request.
            SIPEndPoint localSIPEndPoint = null;
            if (!sipRequest.Header.ProxySendFrom.IsNullOrBlank())
            {
                SIPChannel proxyChannel = m_sipTransport.FindSIPChannel(SIPEndPoint.ParseSIPEndPoint(sipRequest.Header.ProxySendFrom));
                localSIPEndPoint = (proxyChannel != null) ? proxyChannel.SIPChannelEndPoint : null;
            }
            localSIPEndPoint = localSIPEndPoint ?? m_sipTransport.GetDefaultSIPEndPoint(dstSIPEndPoint);

            // Create the single Via header for the outgoing request. It uses the passed in branchid which has been taken from the
            // request that's being forwarded. If this proxy is behind a NAT and the public IP is known that's also set on the Via.
            sipRequest.Header.Vias = new SIPViaSet();
            SIPViaHeader via = new SIPViaHeader(localSIPEndPoint, proxyBranch);
            if (publicIPAddress != null)
            {
                via.Host = publicIPAddress.ToString();
            }
            sipRequest.Header.Vias.PushViaHeader(via);

            if (sipRequest.Method != SIPMethodsEnum.REGISTER)
            {
                AdjustContactHeader(sipRequest, localSIPEndPoint, publicIPAddress);
            }

            // If dispatcher is being used record the transaction so responses are sent to the correct internal socket.
            if (m_dispatcher != null && sipRequest.Method != SIPMethodsEnum.REGISTER && sipRequest.Method != SIPMethodsEnum.ACK)
            {
                //Log("RecordDispatch for " + sipRequest.Method + " " + sipRequest.URI.ToString() + " to " + sipRequest.RemoteSIPEndPoint.ToString() + ".");
                m_dispatcher.RecordDispatch(sipRequest, sipRequest.RemoteSIPEndPoint);
            }

            // Proxy sepecific headers that don't need to be seen by external UAs.
            sipRequest.Header.ProxyReceivedOn = null;
            sipRequest.Header.ProxyReceivedFrom = null;
            sipRequest.Header.ProxySendFrom = null;

            sipRequest.LocalSIPEndPoint = localSIPEndPoint;
            m_sipTransport.SendRequest(dstSIPEndPoint, sipRequest);
        }

        private void AdjustContactHeader(SIPRequest sipRequest, SIPEndPoint localSIPEndPoint, IPAddress publicIPAddress)
        {
            // Set the Contact URI on the outgoing requests depending on which SIP socket the request is being sent on and whether
            // the request is going to an external network.
            if (sipRequest.Header.Contact != null && sipRequest.Header.Contact.Count == 1)
            {
                if (publicIPAddress != null)
                {
                    sipRequest.Header.Contact[0].ContactURI.Host = new IPEndPoint(publicIPAddress, localSIPEndPoint.SocketEndPoint.Port).ToString();
                    sipRequest.Header.Contact[0].ContactURI.Protocol = localSIPEndPoint.SIPProtocol;
                }
                else
                {
                    sipRequest.Header.Contact[0].ContactURI.Host = localSIPEndPoint.SocketEndPoint.ToString();
                    sipRequest.Header.Contact[0].ContactURI.Protocol = localSIPEndPoint.SIPProtocol;
                }
            }
        }

        public void Send(SIPResponse sipResponse)
        {
            m_sipTransport.SendResponse(sipResponse);
        }

        public void Send(SIPResponse sipResponse, string localSIPEndPoint)
        {
            sipResponse.LocalSIPEndPoint = SIPEndPoint.ParseSIPEndPoint(localSIPEndPoint);
            m_sipTransport.SendResponse(sipResponse);
        }

        public void Send(SIPResponse sipResponse, SIPEndPoint localSIPEndPoint)
        {
            sipResponse.LocalSIPEndPoint = localSIPEndPoint;
            m_sipTransport.SendResponse(sipResponse);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sipResponse"></param>
        /// <param name="localSIPEndPoint"></param>
        /// <param name="setContactURIToLocal">If true will set the Contact URI to the URI of the proxy socket the response
        /// is being sent from. This should only be used for INVITE responses where the contact header needs to be updated
        /// to make sure the correct proxy socket is used for in-dialogue requests.</param>
        public void Send(SIPResponse sipResponse, SIPEndPoint localSIPEndPoint, IPAddress publicIPAddress)
        {
            try
            {
                sipResponse.LocalSIPEndPoint = localSIPEndPoint;

                if (sipResponse.Header.CSeqMethod != SIPMethodsEnum.REGISTER)
                {
                    if (sipResponse.Header.Contact != null && sipResponse.Header.Contact.Count == 1)
                    {
                        if (publicIPAddress != null)
                        {
                            sipResponse.Header.Contact[0].ContactURI.Host = new IPEndPoint(publicIPAddress, localSIPEndPoint.SocketEndPoint.Port).ToString();
                            sipResponse.Header.Contact[0].ContactURI.Protocol = localSIPEndPoint.SIPProtocol;
                        }
                        else
                        {
                            sipResponse.Header.Contact[0].ContactURI.Host = localSIPEndPoint.SocketEndPoint.ToString();
                            sipResponse.Header.Contact[0].ContactURI.Protocol = localSIPEndPoint.SIPProtocol;
                        }
                    }
                }

                m_sipTransport.SendResponse(sipResponse);
            }
            catch (Exception excp)
            {
                logger.Error("Exception SIPProxyScriptHelper Send SIPResponse. " + excp);
            }
        }

        /// <summary>
        /// Helper method for dynamic proxy runtime script.
        /// </summary>
        /// <param name="responseCode"></param>
        /// <param name="localEndPoint"></param>
        /// <param name="remoteEndPoint"></param>
        /// <param name="sipRequest"></param>
        public void Respond(SIPRequest sipRequest, SIPResponseStatusCodesEnum responseCode, string reasonPhrase)
        {
            SIPResponse response = SIPTransport.GetResponse(sipRequest, responseCode, reasonPhrase);
            m_sipTransport.SendResponse(response);
        }

        public SIPEndPoint Resolve(SIPRequest sipRequest)
        {
            return m_sipTransport.GetRequestEndPoint(sipRequest, null, true);
        }

        public SIPEndPoint Resolve(SIPURI sipURI)
        {
            return m_sipTransport.GetURIEndPoint(sipURI, false);
        }

        public SIPEndPoint Resolve(SIPResponse sipResponse)
        {
            SIPViaHeader topVia = sipResponse.Header.Vias.TopViaHeader;
            SIPEndPoint dstEndPoint = m_sipTransport.GetHostEndPoint(topVia.ReceivedFromAddress, true);
            dstEndPoint.SIPProtocol = topVia.Transport;
            return dstEndPoint;
        }

        public SIPEndPoint GetDefaultSIPEndPoint(SIPProtocolsEnum protocol)
        {
            return m_sipTransport.GetDefaultSIPEndPoint(protocol);
        }

        public SIPEndPoint DispatcherLookup(SIPRequest sipRequest)
        {
            if (m_dispatcher != null)
            {
                return m_dispatcher.LookupTransactionID(sipRequest);
            }

            return null;
        }

        public SIPEndPoint DispatcherLookup(string branch, SIPMethodsEnum method)
        {
            if (m_dispatcher != null && !branch.IsNullOrBlank())
            {
                return m_dispatcher.LookupTransactionID(branch, method);
            }

            return null;
        }

        public SIPEndPoint GetAppServer()
        {
            return GetAppServer_External();
        }
    }
}
