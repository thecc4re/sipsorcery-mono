//-----------------------------------------------------------------------------
// Filename: SIPRequest.cs
//
// Description: SIP Request.
//
// History:
// 20 Oct 2005	Aaron Clauson	Created.
//
// License: 
// This software is licensed under the BSD License http://www.opensource.org/licenses/bsd-license.php
//
// Copyright (c) 2006 Aaron Clauson (aaronc@blueface.ie), Blue Face Ltd, Dublin, Ireland (www.blueface.ie)
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
//-----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using SIPSorcery.Sys;
using log4net;

#if UNITTEST
using NUnit.Framework;
#endif

namespace SIPSorcery.SIP
{
    /// <bnf>
	///  Method SP Request-URI SP SIP-Version CRLF
	///  *message-header
	///	 CRLF
	///	 [ message-body ]
	///	 
	///	 Methods: REGISTER, INVITE, ACK, CANCEL, BYE, OPTIONS
	///	 SIP-Version: SIP/2.0
	///	 
	///	 SIP-Version    =  "SIP" "/" 1*DIGIT "." 1*DIGIT
	/// </bnf>
	public class SIPRequest
	{
        private static ILog logger = AssemblyState.logger;

        private delegate bool IsLocalSIPSocketDelegate(string socket, SIPProtocolsEnum protocol);

		private static string m_CRLF = SIPConstants.CRLF;
		private static string m_sipFullVersion = SIPConstants.SIP_FULLVERSION_STRING;
		private static string m_sipVersion = SIPConstants.SIP_VERSION_STRING;
		private static int m_sipMajorVersion = SIPConstants.SIP_MAJOR_VERSION;
		private static int m_sipMinorVersion = SIPConstants.SIP_MINOR_VERSION;

		public string SIPVersion = m_sipVersion;
		public int SIPMajorVersion = m_sipMajorVersion;
		public int SIPMinorVersion = m_sipMinorVersion;
		public SIPMethodsEnum Method;
		public string UnknownMethod = null;

		public SIPURI URI;
		public SIPHeader Header;
		public string Body;
        public SIPRoute ReceivedRoute;

		public DateTime Created = DateTime.Now;
		public SIPEndPoint RemoteSIPEndPoint;               // The remote IP socket the request was received from or sent to.
        public SIPEndPoint LocalSIPEndPoint;                // The local SIP socket the request was received on or sent from.

		private SIPRequest()
		{
            //Created++;
        }
			
		public SIPRequest(SIPMethodsEnum method, string uri)
		{
            try
            {
                Method = method;
                URI = SIPURI.ParseSIPURI(uri);
                SIPVersion = m_sipFullVersion;
            }
            catch (Exception excp)
            {
                logger.Error("Exception SIPRequest ctor. " + excp.Message);
                throw;
            }
		}

        public SIPRequest(SIPMethodsEnum method, SIPURI uri)
        {
             //Created++;
             Method = method;
             URI = uri;
             SIPVersion = m_sipFullVersion;
        }

		public static SIPRequest ParseSIPRequest(SIPMessage sipMessage)
		{
            string uriStr = null;

            try
            {
                SIPRequest sipRequest = new SIPRequest();
                sipRequest.LocalSIPEndPoint = sipMessage.LocalSIPEndPoint;
                sipRequest.RemoteSIPEndPoint = sipMessage.RemoteSIPEndPoint;

                string statusLine = sipMessage.FirstLine;

                int firstSpacePosn = statusLine.IndexOf(" ");

                string method = statusLine.Substring(0, firstSpacePosn).Trim();
                sipRequest.Method = SIPMethods.GetMethod(method);
                if (sipRequest.Method == SIPMethodsEnum.UNKNOWN)
                {
                    sipRequest.UnknownMethod = method;
                    logger.Warn("Unknown SIP method received " + sipRequest.UnknownMethod + ".");
                }

                statusLine = statusLine.Substring(firstSpacePosn).Trim();
                int secondSpacePosn = statusLine.IndexOf(" ");

                if (secondSpacePosn != -1)
                {
                    uriStr = statusLine.Substring(0, secondSpacePosn);

                    sipRequest.URI = SIPURI.ParseSIPURI(uriStr);
                    sipRequest.SIPVersion = statusLine.Substring(secondSpacePosn, statusLine.Length - secondSpacePosn).Trim();
                    sipRequest.Header = SIPHeader.ParseSIPHeaders(sipMessage.SIPHeaders);
                    sipRequest.Body = sipMessage.Body;

                    return sipRequest;
                }
                else
                {
                    throw new SIPValidationException(SIPValidationFieldsEnum.Request, "URI was missing on Request.");
                }
            }
            catch (SIPValidationException)
            {
                throw;
            }
            catch (Exception excp)
            {
                logger.Error("Exception parsing SIP Request. " + excp.Message);
                logger.Error(sipMessage.RawMessage);
                throw new SIPValidationException(SIPValidationFieldsEnum.Request, "Unknown error parsing SIP Request");
            }
		}

        public static SIPRequest ParseSIPRequest(string sipMessageStr)
        {
            try
            {
                SIPMessage sipMessage = SIPMessage.ParseSIPMessage(sipMessageStr, null, null);
                return SIPRequest.ParseSIPRequest(sipMessage);
            }
            catch (SIPValidationException)
            {
                throw;
            }
            catch (Exception excp)
            {
                logger.Error("Exception ParseSIPRequest. " + excp.Message);
                logger.Error(sipMessageStr);
                throw new SIPValidationException(SIPValidationFieldsEnum.Request, "Unknown error parsing SIP Request");
            }
        }

		public new string ToString()
		{
			try
			{
				string methodStr = (Method != SIPMethodsEnum.UNKNOWN) ? Method.ToString() : UnknownMethod;
				
				string message = methodStr + " " + URI.ToString() + " " + SIPVersion + m_CRLF + this.Header.ToString();

				if(Body != null)
				{
					message += m_CRLF + Body;
				}
				else
				{
					message += m_CRLF;
				}
			
				return message;
			}
			catch(Exception excp)
			{
				logger.Error("Exception SIPRequest ToString. " + excp.Message);
				throw excp;
			}
		}

        /// <summary>
        /// Creates an identical copy of the SIP Request for the caller.
        /// </summary>
        /// <returns>New copy of the SIPRequest.</returns>
        public SIPRequest Copy()
        {
            return ParseSIPRequest(this.ToString());
        }
		
		public string CreateBranchId()
		{
			string routeStr = (Header.Routes != null) ? Header.Routes.ToString() : null;
			string toTagStr = (Header.To != null) ? Header.To.ToTag : null;
			string fromTagStr = (Header.From != null) ? Header.From.FromTag : null;
			string topViaStr = (Header.Vias != null && Header.Vias.TopViaHeader != null) ? Header.Vias.TopViaHeader.ToString() : null;

			return CallProperties.CreateBranchId(
				SIPConstants.SIP_BRANCH_MAGICCOOKIE,
				toTagStr,
				fromTagStr,
				Header.CallId,
				URI.ToString(),
				topViaStr,
				Header.CSeq,
				routeStr,
				Header.ProxyRequire,
				null);
		}
		
		/// <summary>
		/// Determines if this SIP header is a looped header. The basis for the decision is the branchid in the Via header. If the branchid for a new
		/// header computes to the same branchid as a Via header already in the SIP header then it is considered a loop.
		/// </summary>
		/// <returns>True if this header is a loop otherwise false.</returns>
		public bool IsLoop(string ipAddress, int port, string currentBranchId)
		{			
			foreach(SIPViaHeader viaHeader in Header.Vias.Via)
			{
				if(viaHeader.Host == ipAddress && viaHeader.Port == port)
				{
					if(viaHeader.Branch == currentBranchId)
					{
						return true;
					}
				}
			}
				
			return false;
		}
	
        //~SIPRequest()
        //{
        //    Destroyed++;
        //}
		
		#region Unit testing.

		#if UNITTEST

        [TestFixture]
        public class SIPRequestUnitTest
        {
            private class MockSIPDNSManager
            {
                public static SIPEndPoint Resolve(SIPURI sipURI, bool synchronous)
                {
                    // This assumes the input SIP URI has an IP address as the host!
                    return new SIPEndPoint(sipURI);
                }
            }

            private class MockSIPChannel : SIPChannel
            {
                public MockSIPChannel(IPEndPoint channelEndPoint)
                {
                    m_localSIPEndPoint = new SIPEndPoint(SIPProtocolsEnum.udp, channelEndPoint);
                }

                public override void Send(IPEndPoint destinationEndPoint, string message)
                { }

                public override void Send(IPEndPoint destinationEndPoint, byte[] buffer)
                { }

                public override void Close()
                { }

                public override void Send(IPEndPoint dstEndPoint, byte[] buffer, string serverCN)
                {
                    throw new ApplicationException("This Send method is not available in the MockSIPChannel, please use an alternative overload.");
                }
            }
            
            private List<LocalSIPSocket> m_sipSockets = new List<LocalSIPSocket>();

            private struct LocalSIPSocket
            {
                public string Socket;
                public SIPProtocolsEnum Protocol;

                public LocalSIPSocket(string socket, SIPProtocolsEnum protocol)
                {
                    Socket = socket;
                    Protocol = protocol;
                }
            }

            private bool IsLocalSIPSocket(string socket, SIPProtocolsEnum protocol)
            {
                foreach (LocalSIPSocket sipSocket in m_sipSockets)
                {
                    if (sipSocket.Socket == socket && sipSocket.Protocol == protocol)
                    {
                        return true;
                    }
                }

                return false;
            }


            [TestFixtureSetUp]
            public void Init()
            {
                //SIPRequest.IsLocalSIPSocket += new IsLocalSIPSocketDelegate(IsLocalSIPSocket);
            }

            [TestFixtureTearDown]
            public void Dispose()
            {

            }

            [Test]
            public void SampleTest()
            {
                Console.WriteLine(System.Reflection.MethodBase.GetCurrentMethod().Name);

                Assert.IsTrue(true, "True was false.");
            }

            [Test]
            public void ParseXtenINVITEUnitTest()
            {
                Console.WriteLine("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);

                string sipMsg =
                    "INVITE sip:303@sip.blueface.ie SIP/2.0" + m_CRLF +
                    "Via: SIP/2.0/UDP 192.168.1.2:5065;rport;branch=z9hG4bKFBB7EAC06934405182D13950BD51F001" + m_CRLF +
                    "From: SER Test X <sip:aaronxten@sip.blueface.ie:5065>;tag=196468136" + m_CRLF +
                    "To: <sip:303@sip.blueface.ie>" + m_CRLF +
                    "Contact: <sip:aaronxten@192.168.1.2:5065>" + m_CRLF +
                    "Call-ID: A3DF9A04-0EFE-47E4-98B1-E18AA186F3D6@192.168.1.2" + m_CRLF +
                    "CSeq: 49429 INVITE" + m_CRLF +
                    "Max-Forwards: 70" + m_CRLF +
                    "Content-Type: application/sdp" + m_CRLF +
                    "User-Agent: X-PRO release 1103v" + m_CRLF +
                    "Content-Length: 271" + m_CRLF +
                    m_CRLF +
                    "v=0" + m_CRLF +
                    "o=aaronxten 423137371 423137414 IN IP4 192.168.1.2" + m_CRLF +
                    "s=X-PRO" + m_CRLF +
                    "c=IN IP4 192.168.1.2" + m_CRLF +
                    "t=0 0" + m_CRLF +
                    "m=audio 8004 RTP/AVP 0 8 3 97 101" + m_CRLF +
                    "a=rtpmap:0 pcmu/8000" + m_CRLF +
                    "a=rtpmap:8 pcma/8000" + m_CRLF +
                    "a=rtpmap:3 gsm/8000" + m_CRLF +
                    "a=rtpmap:97 speex/8000" + m_CRLF +
                    "a=rtpmap:101 telephone-event/8000" + m_CRLF +
                    "a=fmtp:101 0-15" + m_CRLF;

                SIPMessage sipMessage = SIPMessage.ParseSIPMessage(Encoding.UTF8.GetBytes(sipMsg), null, null);
                SIPRequest inviteReq = SIPRequest.ParseSIPRequest(sipMessage);

                if (inviteReq.Body != null)
                {
                    Console.WriteLine("Body Length = " + inviteReq.Body.Length + ".");
                }
                Console.WriteLine("Body:\r\n" + inviteReq.Body + ".");

                Assert.IsTrue(inviteReq.Method == SIPMethodsEnum.INVITE, "The SIP request method was not parsed correctly.");
                Assert.IsTrue(inviteReq.SIPMajorVersion == 2, "The SIP Major version was not parsed correctly.");
                Assert.IsTrue(inviteReq.SIPMinorVersion == 0, "The SIP Minor version was not parsed correctly.");
                Assert.IsTrue(inviteReq.URI.User == "303", "The SIP request URI Name was not parsed correctly.");
                Assert.IsTrue(inviteReq.URI.Host == "sip.blueface.ie", "The SIP request URI Host was not parsed correctly.");
                Assert.IsTrue(inviteReq.Body != null && inviteReq.Body.Length == 271, "The SIP content body was not parsed correctly.");

                Console.WriteLine("-----------------------------------------");
            }
             
            [Test]
            public void ParseAsteriskACKUnitTest()
            {
                Console.WriteLine("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);

                string sipMsg =
                    "ACK sip:303@213.168.225.133 SIP/2.0" + m_CRLF +
                    "Via: SIP/2.0/UDP 192.168.1.2:5065;rport;branch=z9hG4bK3667AD800F014BD685C2E0A8B2AB9D0F" + m_CRLF +
                    "From: bluesipd <sip:bluesipd@bluesipd:5065>;tag=396492091" + m_CRLF +
                    "To: <sip:303@bluesipd>;tag=as022cbb0e" + m_CRLF +
                    "Contact: <sip:bluesipd@192.168.1.2:5065>" + m_CRLF +
                    "Route: <sip:213.168.225.135;lr>" + m_CRLF +
                    "Call-ID: EDA17D42-034E-438B-8467-18DF1E4678A7@192.168.1.2" + m_CRLF +
                    "CSeq: 39639 ACK" + m_CRLF +
                    "Max-Forwards: 70" + m_CRLF +
                    "Content-Length: 0" + m_CRLF + m_CRLF;

                SIPMessage sipMessage = SIPMessage.ParseSIPMessage(Encoding.UTF8.GetBytes(sipMsg), null, null);
                SIPRequest ackReq = SIPRequest.ParseSIPRequest(sipMessage);

                Assert.IsTrue(ackReq.Method == SIPMethodsEnum.ACK, "The SIP request method was not parsed correctly.");
                Assert.IsTrue(ackReq.SIPMajorVersion == 2, "The SIP Major version was not parsed correctly.");
                Assert.IsTrue(ackReq.SIPMinorVersion == 0, "The SIP Minor version was not parsed correctly.");
                Assert.IsTrue(ackReq.URI.User == "303", "The SIP request URI was not parsed correctly.");
                Assert.IsTrue(ackReq.URI.Host == "213.168.225.133", "The SIP request URI Host was not parsed correctly.");

                Console.WriteLine("-----------------------------------------");
            }

            [Test]
            public void ParseCiscoACKUnitTest()
            {
                Console.WriteLine("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);

                string sipMsg =
                    "ACK sip:303@213.168.225.133:5061 SIP/2.0" + m_CRLF +
                    "Via: SIP/2.0/UDP 89.100.92.186:5060;branch=z9hG4bK1254dba9" + m_CRLF +
                    "From: dev <sip:aaron@azaclauson.dyndns.org>" + m_CRLF +
                    "To: <sip:303@azaclauson.dyndns.org>;tag=as108bd3ae" + m_CRLF +
                    "Call-ID: 0013c339-acec0041-61c7c61e-3eb0b7c0@89.100.92.186" + m_CRLF +
                    "Max-Forwards: 70" + m_CRLF +
                    "Date: Mon, 07 Aug 2006 14:57:40 GMT" + m_CRLF +
                    "CSeq: 102 ACK" + m_CRLF +
                    "User-Agent: Cisco-CP7960G/8.0" + m_CRLF +
                    "Route: <sip:89.100.92.186:6060;lr>" + m_CRLF +
                    "Proxy-Authorization: Digest username=\"aaron\",realm=\"sip.blueface.ie\",uri=\"sip:303@azaclauson.dyndns.org\",response=\"638c8fb6186fe865e80f6232cc417c3f\",nonce=\"44f121a2\",algorithm=md5" + m_CRLF +
                    "Remote-Party-ID: \"dev\" <sip:aaron@89.100.92.186>;party=calling;id-type=subscriber;privacy=off;screen=yes" + m_CRLF +
                    "Content-Length: 0" + m_CRLF + m_CRLF;

                SIPMessage sipMessage = SIPMessage.ParseSIPMessage(Encoding.UTF8.GetBytes(sipMsg), null, null);
                SIPRequest ackReq = SIPRequest.ParseSIPRequest(sipMessage);

                Assert.IsTrue(ackReq.Method == SIPMethodsEnum.ACK, "The SIP request method was not parsed correctly.");
                Assert.IsTrue(ackReq.SIPMajorVersion == 2, "The SIP Major version was not parsed correctly.");
                Assert.IsTrue(ackReq.SIPMinorVersion == 0, "The SIP Minor version was not parsed correctly.");
                Assert.IsTrue(ackReq.URI.User == "303", "The SIP request URI was not parsed correctly.");
                Assert.IsTrue(ackReq.URI.Host == "213.168.225.133:5061", "The SIP request URI Host was not parsed correctly.");

                Console.WriteLine("-----------------------------------------");
            }

            [Test]
            public void ParseXtenByeUnitTest()
            {
                Console.WriteLine("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);

                string sipMsg =
                    "BYE sip:303@213.168.225.133 SIP/2.0" + m_CRLF +
                    "Via: SIP/2.0/UDP 192.168.1.2:5065;rport;branch=z9hG4bK7F023DE3FF8941008DE7DCE71A20CB78" + m_CRLF +
                    "From: bluesipd <sip:bluesipd@bluesipd:5065>;tag=396492091" + m_CRLF +
                    "To: <sip:303@bluesipd>;tag=as022cbb0e" + m_CRLF +
                    "Contact: <sip:bluesipd@192.168.1.2:5065>" + m_CRLF +
                    "Route: <sip:213.168.225.135;lr>" + m_CRLF +
                    "Call-ID: EDA17D42-034E-438B-8467-18DF1E4678A7@192.168.1.2" + m_CRLF +
                    "CSeq: 39640 BYE" + m_CRLF +
                    "Max-Forwards: 70" + m_CRLF +
                    "User-Agent: X-PRO release 1103v" + m_CRLF +
                    "Content-Length: 0" + m_CRLF + m_CRLF;

                SIPMessage sipMessage = SIPMessage.ParseSIPMessage(Encoding.UTF8.GetBytes(sipMsg), null, null);
                SIPRequest byeReq = SIPRequest.ParseSIPRequest(sipMessage);

                Assert.IsTrue(byeReq.Method == SIPMethodsEnum.BYE, "The SIP request method was not parsed correctly.");
                Assert.IsTrue(byeReq.SIPMajorVersion == 2, "The SIP Major version was not parsed correctly.");
                Assert.IsTrue(byeReq.SIPMinorVersion == 0, "The SIP Minor version was not parsed correctly.");
                Assert.IsTrue(byeReq.URI.User == "303", "The SIP request URI name was not parsed correctly.");

                Console.WriteLine("-----------------------------------------");
            }

            [Test]
            public void ParseAsteriskBYEUnitTest()
            {
                Console.WriteLine("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);

                string sipMsg =
                    "BYE sip:bluesipd@192.168.1.2:5065 SIP/2.0" + m_CRLF +
                    "Via: SIP/2.0/UDP 213.168.225.133:5060;branch=z9hG4bK74ab714b;rport" + m_CRLF +
                    "Route: <sip:bluesipd@192.168.1.2:5065>" + m_CRLF +
                    "From: <sip:303@bluesipd>;tag=as6a65fae3" + m_CRLF +
                    "To: bluesipd <sip:bluesipd@bluesipd:5065>;tag=1898247079" + m_CRLF +
                    "Contact: <sip:303@213.168.225.133>" + m_CRLF +
                    "Call-ID: 80B34165-8C89-4623-B862-40AFB1884071@192.168.1.2" + m_CRLF +
                    "CSeq: 102 BYE" + m_CRLF +
                    "User-Agent: asterisk" + m_CRLF +
                    "Content-Length: 0" + m_CRLF + m_CRLF;

                SIPMessage sipMessage = SIPMessage.ParseSIPMessage(Encoding.UTF8.GetBytes(sipMsg), null, null);
                SIPRequest byeReq = SIPRequest.ParseSIPRequest(sipMessage);

                Assert.IsTrue(byeReq.Method == SIPMethodsEnum.BYE, "The SIP request method was not parsed correctly.");
                Assert.IsTrue(byeReq.SIPMajorVersion == 2, "The SIP Major version was not parsed correctly.");
                Assert.IsTrue(byeReq.SIPMinorVersion == 0, "The SIP Minor version was not parsed correctly.");
                Assert.IsTrue(byeReq.URI.User == "bluesipd", "The SIP request URI Name was not parsed correctly.");
                Assert.IsTrue(byeReq.URI.Host == "192.168.1.2:5065", "The SIP request URI Host was not parsed correctly.");

                Console.WriteLine("-----------------------------------------");
            }

            [Test]
            public void TopRouteUnitTest()
            {
                Console.WriteLine("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);

                string sipMsg =
                    "BYE sip:bluesipd@192.168.1.2:5065 SIP/2.0" + m_CRLF +
                    "Via: SIP/2.0/UDP 213.168.225.133:5060;branch=z9hG4bK483ca249;rport" + m_CRLF +
                    "Route: <sip:220.240.255.198:64300;lr>,<sip:bluesipd@192.168.1.2:5065>" + m_CRLF +
                    "From: <sip:303@bluesipd>;tag=as7a10c774" + m_CRLF +
                    "To: bluesipd <sip:bluesipd@bluesipd:5065>;tag=2561975990" + m_CRLF +
                    "Contact: <sip:303@213.168.225.133>" + m_CRLF +
                    "Call-ID: D9D08936-5455-476C-A5A2-A069D4B8DBFF@192.168.1.2" + m_CRLF +
                    "CSeq: 102 BYE" + m_CRLF +
                    "User-Agent: asterisk" + m_CRLF +
                    "Content-Length: 0" + m_CRLF + m_CRLF;

                SIPMessage sipMessage = SIPMessage.ParseSIPMessage(Encoding.UTF8.GetBytes(sipMsg), null, null);
                SIPRequest byeReq = SIPRequest.ParseSIPRequest(sipMessage);

                SIPRoute topRoute = byeReq.Header.Routes.PopRoute();
                Assert.IsTrue(topRoute.Host == "220.240.255.198:64300", "The top route was not parsed correctly, top route IP address was " + topRoute.Host + ".");

                Console.WriteLine("-----------------------------------------");
            }

            [Test]
            public void SubscribeRequestUnitTest()
            {
                Console.WriteLine("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);

                string sipMsg =
                    "SUBSCRIBE sip:0123456@127.0.0.1 SIP/2.0" + m_CRLF +
                    "Via: SIP/2.0/UDP 192.168.1.10:15796" + m_CRLF +
                    "Max-Forwards: 70" + m_CRLF +
                    "From: \"user@sip.domain.com\" <sip:user@sipdomain.com>;tag=a6cf9fe4cdee4e1cad88240403b95669;epid=1bb41c1f89" + m_CRLF +
                    "To: <sip:0123456@sip.blueface.ie>;tag=as211b359e" + m_CRLF +
                    "Call-ID: abebae2060d747c3ba11a0d0cde9ab0b" + m_CRLF +
                    "CSeq: 81 SUBSCRIBE" + m_CRLF +
                    "Contact: <sip:192.168.1.10:15796>" + m_CRLF +
                    "User-Agent: RTC/1.3" + m_CRLF +
                    "Event: presence" + m_CRLF +
                    "Accept: application/xpidf+xml, text/xml+msrtc.pidf, application/pidf+xml" + m_CRLF +
                    "Supported: com.microsoft.autoextend" + m_CRLF +
                    "Supported: ms-benotify" + m_CRLF +
                    "Proxy-Require: ms-benotify" + m_CRLF +
                    "Content-Length: 0" + m_CRLF + m_CRLF;

                SIPMessage sipMessage = SIPMessage.ParseSIPMessage(Encoding.UTF8.GetBytes(sipMsg), null, null);
                SIPRequest subscribeReq = SIPRequest.ParseSIPRequest(sipMessage);

                Console.WriteLine(subscribeReq.ToString());

                Console.WriteLine("-----------------------------------------");
            }

            [Test]
            public void SpaceInNamesRequestUnitTest()
            {
                Console.WriteLine("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);

                string sipMsg =
                    "REGISTER sip:Blue Face SIP/2.0" + m_CRLF +
                    "Via: SIP/2.0/UDP 127.0.0.1:1720;branch=z9hG4bKlgnUQcaywCOaPcXR" + m_CRLF +
                    "Max-Forwards: 70" + m_CRLF +
                    "User-Agent: PA168S" + m_CRLF +
                    "From: \"user\" <sip:user@Blue Face>;tag=81swjAV7dHG1yjd5" + m_CRLF +
                    "To: \"user\" <sip:user@Blue Face>" + m_CRLF +
                    "Call-ID: DHZVs1HFuMoTQ6LO@82.114.95.1" + m_CRLF +
                    "CSeq: 15754 REGISTER" + m_CRLF +
                    "Contact: <sip:user@127.0.0.1:1720>" + m_CRLF +
                    "Expires: 30" + m_CRLF +
                    "Content-Length: 0" + m_CRLF + m_CRLF;

                SIPMessage sipMessage = SIPMessage.ParseSIPMessage(Encoding.UTF8.GetBytes(sipMsg), null, null);
                SIPRequest registerReq = SIPRequest.ParseSIPRequest(sipMessage);

                Console.WriteLine(registerReq.ToString());

                Console.WriteLine("-----------------------------------------");
            }

            /// <summary>
            /// Error on this request is a non-numeric port on the Via header.
            /// </summary>
            [Test]
            [ExpectedException(typeof(SIPValidationException))]
            public void DodgyAastraRequestUnitTest()
            {
                Console.WriteLine("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);

                string sipMsg =
                    "REGISTER sip:sip.blueface.ie SIP/2.0" + m_CRLF +
                    "Via: SIP/2.0/UDP 83.70.206.42:5060port;branch=z9hG4bK77c61058c08e3a6737e4c76e6241cc3f" + m_CRLF +
                    "To: <sip:100001@sip.blueface.ie:5060>" + m_CRLF +
                    "From: <sip:100001@sip.blueface.ie:5060>;tag=AI5A09C508-2F0401CDFF625DD3" + m_CRLF +
                    "Call-ID: AI5A09C4D6-3122395B17A0C101@192.168.14.250" + m_CRLF +
                    "CSeq: 25015 REGISTER" + m_CRLF +
                    "Max-Forwards: 70" + m_CRLF +
                    "Expires: 3000" + m_CRLF +
                    "Contact: <sip:100001@83.70.206.42:5060\n" +
                    "Allow: ACK,BYE,CANCEL,INVITE,NOTIFY,OPTIONS,REFER" + m_CRLF +
                    "User-Agent: Aastra Intelligate" + m_CRLF +
                    "Content-Length: 0" + m_CRLF + m_CRLF;

                SIPMessage sipMessage = SIPMessage.ParseSIPMessage(Encoding.UTF8.GetBytes(sipMsg), null, null);
                SIPRequest registerReq = SIPRequest.ParseSIPRequest(sipMessage);

                Console.WriteLine("-----------------------------------------");
            }

            [Test]
            public void NetgearInviteRequestUnitTest()
            {
                Console.WriteLine("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);

                string sipMsg =
                    "INVITE sip:12345@sip.domain.com:5060;TCID-0 SIP/2.0" + m_CRLF +
                    "From: UNAVAILABLE<sip:user@sip.domain.com:5060>;tag=c0a83dfe-13c4-26bf01-975a21d0-2d8a" + m_CRLF +
                    "To: <sip:1234@sipdomain.com:5060>" + m_CRLF +
                    "Call-ID: 94b6e3f8-c0a83dfe-13c4-26bf01-975a21ce-52c@sip.domain.com" + m_CRLF +
                    "CSeq: 1 INVITE" + m_CRLF +
                    "Via: SIP/2.0/UDP 86.9.84.23:5060;branch=z9hG4bK-26bf01-975a21d0-1ffb" + m_CRLF +
                    "Max-Forwards: 70" + m_CRLF +
                    "User-Agent: TA612V-V1.2_54" + m_CRLF +
                    "Supported: timer,replaces" + m_CRLF +
                    "Contact: <sip:user@88.8.88.88:5060>" + m_CRLF +
                    "Content-Type: application/SDP" + m_CRLF +
                    "Content-Length: 386" + m_CRLF +
                    m_CRLF +
                    "v=0" + m_CRLF +
                    "o=b0000 613 888 IN IP4 88.8.88.88" + m_CRLF +
                    "s=SIP Call" + m_CRLF +
                    "c=IN IP4 88.8.88.88" + m_CRLF +
                    "t=0 0" + m_CRLF +
                    "m=audio 10000 RTP/AVP 0 101 18 100 101 2 103 8" + m_CRLF +
                    "a=fmtp:101 0-15" + m_CRLF +
                    "a=fmtp:18 annexb=no" + m_CRLF +
                    "a=sendrecv" + m_CRLF +
                    "a=rtpmap:0 PCMU/8000" + m_CRLF +
                    "a=rtpmap:101 telephone-event/8000" + m_CRLF +
                    "a=rtpmap:18 G729/8000" + m_CRLF +
                    "a=rtpmap:100 G726-16/8000" + m_CRLF +
                    "a=rtpmap:101 G726-24/8000" + m_CRLF +
                    "a=rtpmap:2 G726-32/8000" + m_CRLF +
                    "a=rtpmap:103 G726-40/8000" + m_CRLF +
                    "a=rtpmap:8 PCMA/8000";

                SIPMessage sipMessage = SIPMessage.ParseSIPMessage(Encoding.UTF8.GetBytes(sipMsg), null, null);
                SIPRequest inviteReq = SIPRequest.ParseSIPRequest(sipMessage);

                Console.WriteLine(inviteReq.ToString());

                Console.WriteLine("-----------------------------------------");
            }

            [Test]
            public void RTCRegisterRequestUnitTest()
            {
                Console.WriteLine("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);

                string sipMsg =
                    "REGISTER sip:sip.blueface.ie SIP/2.0" + m_CRLF +
                    "Via: SIP/2.0/UDP 192.168.1.10:15796" + m_CRLF +
                    "Max-Forwards: 70" + m_CRLF +
                    "From: <sip:user@sip.blueface.ie>;tag=1a52c38c46e3439c8b4fe8a58f5ae834;epid=1bb41c1f89" + m_CRLF +
                    "To: <sip:user@sip.blueface.ie>" + m_CRLF +
                    "Call-ID: aeb2c6c905a84610a54de60bb6ef476c" + m_CRLF +
                    "CSeq: 417 REGISTER" + m_CRLF +
                    "Contact: <sip:192.168.1.10:15796>;methods=\"INVITE, MESSAGE, INFO, SUBSCRIBE, OPTIONS, BYE, CANCEL, NOTIFY, ACK, REFER, BENOTIFY\"" + m_CRLF +
                    "User-Agent: RTC/1.3.5470 (Messenger 5.1.0680)" + m_CRLF +
                    "Event: registration" + m_CRLF +
                    "Allow-Events: presence" + m_CRLF +
                    "Content-Length: 0" + m_CRLF + m_CRLF;

                SIPMessage sipMessage = SIPMessage.ParseSIPMessage(Encoding.UTF8.GetBytes(sipMsg), null, null);
                SIPRequest registerReq = SIPRequest.ParseSIPRequest(sipMessage);

                Console.WriteLine("-----------------------------------------");
            }

            [Test]
            public void CiscoRegisterRequest()
            {
                Console.WriteLine("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);

                string sipMsg =
                    "REGISTER sip:194.213.29.11 SIP/2.0" + m_CRLF +
                    "Via: SIP/2.0/UDP 86.9.88.23:5060;branch=z9hG4bK15dbeda2" + m_CRLF +
                    "From: <sip:sip2@194.213.29.11;user=phone>" + m_CRLF +
                    "To: <sip:sip2@194.213.29.11;user=phone>" + m_CRLF +
                    "Call-ID: 0013c339-acec0005-7488d654-42a83bd0@192.168.1.100" + m_CRLF +
                    "Date: Sat, 22 Apr 2006 00:47:31 GMT" + m_CRLF +
                    "CSeq: 10389 REGISTER" + m_CRLF +
                    "User-Agent: CSCO/7" + m_CRLF +
                    "Contact: <sip:sip2@86.9.88.23:5060;user=phone>" + m_CRLF +
                    "Content-Length: 0" + m_CRLF +
                    "Expires: 3600" + m_CRLF + m_CRLF;

                SIPMessage sipMessage = SIPMessage.ParseSIPMessage(Encoding.UTF8.GetBytes(sipMsg), null, null);
                SIPRequest registerReq = SIPRequest.ParseSIPRequest(sipMessage);
            }

            [Test]
            public void AuthenticatedRegisterRequestUnitTest()
            {
                Console.WriteLine("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);

                string sipMsg =
                    "REGISTER sip:blueface.ie SIP/2.0" + m_CRLF +
                    "Via: SIP/2.0/UDP 86.9.88.23:10060;branch=z9hG4bK1DFDB76492E74691A3DF68AC672DAA7C" + m_CRLF +
                    "From: Aaron <sip:aaronxten@blueface.ie>;tag=2090971807" + m_CRLF +
                    "To: Aaron <sip:aaronxten@blueface.ie>" + m_CRLF +
                    "Call-ID: 19CBFF5EB6CB4668A29BEF0C3DC49910@blueface.ie" + m_CRLF +
                    "CSeq: 24291 REGISTER" + m_CRLF +
                    "Max-Forwards: 70" + m_CRLF +
                    "Contact: \"Aaron\" <sip:aaronxten@86.9.88.23:10060>" + m_CRLF +
                    "User-Agent: X-PRO release 1103v" + m_CRLF +
                    "Expires: 1800" + m_CRLF +
                    "Authorization: Digest realm=\"sip.blueface.ie\",nonce=\"1694683214\",username=\"aaronxten\",response=\"85f2089ac958e69ce4d74f5ae72a9a5f\",uri=\"sip:blueface.ie\"" + m_CRLF +
                    m_CRLF;

                SIPMessage sipMessage = SIPMessage.ParseSIPMessage(Encoding.UTF8.GetBytes(sipMsg), null, null);
                SIPRequest registerReq = SIPRequest.ParseSIPRequest(sipMessage);

                Console.WriteLine(registerReq.ToString());

                SIPAuthenticationHeader authHeader = registerReq.Header.AuthenticationHeader;

                Assert.IsTrue(authHeader != null, "The Authorization header was not correctly extracted from the SIP Register Request.");
                Assert.IsTrue(authHeader.SIPDigest.Nonce == "1694683214", "The Authorization header nonce was not correctly extracted from the SIP Register Request, header nonce = " + authHeader.SIPDigest.Nonce + ".");
                Assert.IsTrue(authHeader.SIPDigest.Realm == "sip.blueface.ie", "The Authorization header realm was not correctly extracted from the SIP Register Request.");
                Assert.IsTrue(authHeader.SIPDigest.Username == "aaronxten", "The Authorization username nonce was not correctly extracted from the SIP Register Request, header username = " + authHeader.SIPDigest.Username + ".");
                Assert.IsTrue(authHeader.SIPDigest.URI == "sip:blueface.ie", "The Authorization header URI was not correctly extracted from the SIP Register Request.");
                Assert.IsTrue(authHeader.SIPDigest.Response == "85f2089ac958e69ce4d74f5ae72a9a5f", "The Authorization header response was not correctly extracted from the SIP Register Request.");
            }

            [Test]
            public void MicrosoftMessengerRegisterRequestUnitTest()
            {
                Console.WriteLine("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);

                string sipMsg =
                    "REGISTER sip:aaronmsn SIP/2.0" + m_CRLF +
                    "Via: SIP/2.0/UDP 192.168.1.31:16879" + m_CRLF +
                    "Max-Forwards: 70" + m_CRLF +
                    "From: <sip:aaronmsn>;tag=27359cb6dcdb4b8e9570dd9fc4b09c14;epid=5649ab5588" + m_CRLF +
                    "To: <sip:aaronmsn>" + m_CRLF +
                    "Call-ID: 19b7a4c8c6d647b19afe031df5e91332@192.168.1.31" + m_CRLF +
                    "CSeq: 1 REGISTER" + m_CRLF +
                    "Contact: <sip:192.168.1.31:16879>;methods=\"INVITE, MESSAGE, INFO, SUBSCRIBE, OPTIONS, BYE, CANCEL, NOTIFY, ACK, REFER\"" + m_CRLF +
                    "User-Agent: RTC/1.2.4949" + m_CRLF +
                    "Event: registration" + m_CRLF +
                    "Allow-Events: presence" + m_CRLF +
                    "Content-Length: 0" + m_CRLF + m_CRLF;

                SIPMessage sipMessage = SIPMessage.ParseSIPMessage(Encoding.UTF8.GetBytes(sipMsg), null, null);
                SIPRequest registerReq = SIPRequest.ParseSIPRequest(sipMessage);

                Console.WriteLine(registerReq.ToString());
            }

            [Test]
            public void CreateBranchIdUnitTest()
            {
                Console.WriteLine("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);

                string sipMsg =
                    "INVITE sip:303@sip.blueface.ie SIP/2.0" + m_CRLF +
                    "Via: SIP/2.0/UDP 192.168.1.2:5065;rport;branch=z9hG4bKFBB7EAC06934405182D13950BD51F001" + m_CRLF +
                    "From: SER Test X <sip:aaronxten@sip.blueface.ie:5065>;tag=196468136" + m_CRLF +
                    "To: <sip:303@sip.blueface.ie>" + m_CRLF +
                    "Contact: <sip:aaronxten@192.168.1.2:5065>" + m_CRLF +
                    "Call-ID: A3DF9A04-0EFE-47E4-98B1-E18AA186F3D6@192.168.1.2" + m_CRLF +
                    "CSeq: 49429 INVITE" + m_CRLF +
                    "Max-Forwards: 70" + m_CRLF +
                    "Content-Type: application/sdp" + m_CRLF +
                    "User-Agent: X-PRO release 1103v" + m_CRLF +
                    m_CRLF;

                SIPMessage sipMessage = SIPMessage.ParseSIPMessage(Encoding.UTF8.GetBytes(sipMsg), null, null);
                SIPRequest inviteReq = SIPRequest.ParseSIPRequest(sipMessage);

                string branchId = CallProperties.CreateBranchId();

                Console.WriteLine("branchid=" + branchId);

                int length = branchId.Length - SIPConstants.SIP_BRANCH_MAGICCOOKIE.Length;
                Console.WriteLine("length=" + length);

                Assert.IsTrue(branchId != null, "The branchid was not created correctly.");

                Console.WriteLine("-----------------------------------------");
            }

            /*[Test]
            public void LoopDetectUnitTest()
            {
                Console.WriteLine("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);
               
                string sipMsg = 
                    "INVITE sip:303@sip.blueface.ie SIP/2.0" + m_CRLF +
                    "Via: SIP/2.0/UDP 192.168.1.2:5065;rport;branch=z9hG4bKFBB7EAC06934405182D13950BD51F001" + m_CRLF +
                    "From: SER Test X <sip:aaronxten@sip.blueface.ie:5065>;tag=196468136" + m_CRLF +
                    "To: <sip:303@sip.blueface.ie>" + m_CRLF +
                    "Contact: <sip:aaronxten@192.168.1.2:5065>" + m_CRLF +
                    "Call-ID: A3DF9A04-0EFE-47E4-98B1-E18AA186F3D6@192.168.1.2" + m_CRLF +
                    "CSeq: 49429 INVITE" + m_CRLF +
                    "Max-Forwards: 70" + m_CRLF +
                    "Content-Type: application/sdp" + m_CRLF +
                    "User-Agent: X-PRO release 1103v" + m_CRLF +
                    m_CRLF;

                SIPMessage sipMessage = SIPMessage.ParseSIPMessage(Encoding.UTF8.GetBytes(sipMsg), null);
                SIPRequest inviteReq = SIPRequest.ParseSIPRequest(sipMessage);

                string branchId = inviteReq.CreateBranchId();
				
                SIPViaHeader requestVia = new SIPViaHeader("192.168.1.2", 5065, branchId);
                
                inviteReq.Header.Via.PushViaHeader(requestVia);
				
                Assert.IsTrue(inviteReq.IsLoop("192.168.1.2", 5065, branchId), "The SIP request was not correctly detected as a loop.");

                Console.WriteLine("-----------------------------------------");
            }*/

            [Test]
            public void LooseRouteForProxyUnitTest()
            {
                Console.WriteLine("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);

                string sipMsg =
                    "INVITE sip:303@sip.blueface.ie SIP/2.0" + m_CRLF +
                    "Via: SIP/2.0/UDP 192.168.1.2:5065;rport;branch=z9hG4bKFBB7EAC06934405182D13950BD51F001" + m_CRLF +
                    "Route: <sip:82.195.148.216:5062;lr>,<sip:89.100.92.186:45270;lr>" + m_CRLF +
                    "From: SER Test X <sip:aaronxten@sip.blueface.ie:5065>;tag=196468136" + m_CRLF +
                    "To: <sip:303@sip.blueface.ie>" + m_CRLF +
                    "Contact: <sip:aaronxten@192.168.1.2:5065>" + m_CRLF +
                    "Call-ID: A3DF9A04-0EFE-47E4-98B1-E18AA186F3D6@192.168.1.2" + m_CRLF +
                    "CSeq: 49429 INVITE" + m_CRLF +
                    "Max-Forwards: 70" + m_CRLF +
                    "Content-Type: application/sdp" + m_CRLF +
                    "User-Agent: X-PRO release 1103v" + m_CRLF +
                    m_CRLF;

                SIPMessage sipMessage = SIPMessage.ParseSIPMessage(Encoding.UTF8.GetBytes(sipMsg), null, null);
                SIPRequest inviteReq = SIPRequest.ParseSIPRequest(sipMessage);

                MockSIPChannel mockSIPChannel = new MockSIPChannel(IPSocket.ParseSocketString("82.195.148.216:5062"));
                SIPTransport mockSIPTransport = new SIPTransport(MockSIPDNSManager.Resolve, null, mockSIPChannel, false);

                mockSIPTransport.PreProcessRouteInfo(inviteReq);

                Assert.IsTrue(inviteReq.URI.ToString() == "sip:303@sip.blueface.ie", "The request URI was incorrectly modified.");
                Assert.IsTrue(inviteReq.Header.Routes.TopRoute.ToString() == "<sip:89.100.92.186:45270;lr>", "The request route information was not correctly preprocessed.");
                Assert.IsTrue(inviteReq.Header.Routes.Length == 1, "The route set was an incorrect length.");
            }

            [Test]
            public void LooseRouteForProxyMultipleContactsUnitTest()
            {
                Console.WriteLine("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);

                string sipMsg =
                    "INVITE sip:303@sip.blueface.ie SIP/2.0" + m_CRLF +
                    "Via: SIP/2.0/UDP 192.168.1.2:5065;rport;branch=z9hG4bKFBB7EAC06934405182D13950BD51F001" + m_CRLF +
                    "Route: <sip:82.195.148.216:5062;lr>,<sip:89.100.92.186:45270;lr>" + m_CRLF +
                    "From: SER Test X <sip:aaronxten@sip.blueface.ie:5065>;tag=196468136" + m_CRLF +
                    "To: <sip:303@sip.blueface.ie>" + m_CRLF +
                    "Contact: <sip:aaronxten@192.168.1.2:5065>" + m_CRLF +
                    "Call-ID: A3DF9A04-0EFE-47E4-98B1-E18AA186F3D6@192.168.1.2" + m_CRLF +
                    "CSeq: 49429 INVITE" + m_CRLF +
                    "Max-Forwards: 70" + m_CRLF +
                    "Content-Type: application/sdp" + m_CRLF +
                    "User-Agent: X-PRO release 1103v" + m_CRLF +
                    m_CRLF;

                SIPMessage sipMessage = SIPMessage.ParseSIPMessage(Encoding.UTF8.GetBytes(sipMsg), null, null);
                SIPRequest inviteReq = SIPRequest.ParseSIPRequest(sipMessage);

                SIPTransport mockSIPTransport = new SIPTransport(MockSIPDNSManager.Resolve, null, false);
                mockSIPTransport.AddSIPChannel(new MockSIPChannel(IPSocket.ParseSocketString("82.195.148.216:5061")));
                mockSIPTransport.AddSIPChannel(new MockSIPChannel(IPSocket.ParseSocketString("82.195.148.216:5062")));

                mockSIPTransport.PreProcessRouteInfo(inviteReq);

                Assert.IsTrue(inviteReq.URI.ToString() == "sip:303@sip.blueface.ie", "The request URI was incorrectly modified.");
                Assert.IsTrue(inviteReq.Header.Routes.TopRoute.ToString() == "<sip:89.100.92.186:45270;lr>", "The request route information was not correctly preprocessed.");
                Assert.IsTrue(inviteReq.Header.Routes.Length == 1, "The route set was an incorrect length.");
            }

            [Test]
            public void LooseRouteNotForProxyUnitTest()
            {
                Console.WriteLine("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);

                string sipMsg =
                    "INVITE sip:303@sip.blueface.ie SIP/2.0" + m_CRLF +
                    "Via: SIP/2.0/UDP 192.168.1.2:5065;rport;branch=z9hG4bKFBB7EAC06934405182D13950BD51F001" + m_CRLF +
                    "Route: <sip:82.195.148.216:5062;lr>,<sip:89.100.92.186:45270;lr>" + m_CRLF +
                    "From: SER Test X <sip:aaronxten@sip.blueface.ie:5065>;tag=196468136" + m_CRLF +
                    "To: <sip:303@sip.blueface.ie>" + m_CRLF +
                    "Contact: <sip:aaronxten@192.168.1.2:5065>" + m_CRLF +
                    "Call-ID: A3DF9A04-0EFE-47E4-98B1-E18AA186F3D6@192.168.1.2" + m_CRLF +
                    "CSeq: 49429 INVITE" + m_CRLF +
                    "Max-Forwards: 70" + m_CRLF +
                    "Content-Type: application/sdp" + m_CRLF +
                    "User-Agent: X-PRO release 1103v" + m_CRLF +
                    m_CRLF;

                SIPMessage sipMessage = SIPMessage.ParseSIPMessage(Encoding.UTF8.GetBytes(sipMsg), null, null);
                SIPRequest inviteReq = SIPRequest.ParseSIPRequest(sipMessage);

                SIPTransport mockSIPTransport = new SIPTransport(MockSIPDNSManager.Resolve, null, false);
                mockSIPTransport.AddSIPChannel(new MockSIPChannel(IPSocket.ParseSocketString("10.10.10.10:5060")));
       
                mockSIPTransport.PreProcessRouteInfo(inviteReq);

                Assert.IsTrue(inviteReq.URI.ToString() == "sip:303@sip.blueface.ie", "The request URI was incorrectly modified.");
                Assert.IsTrue(inviteReq.Header.Routes.TopRoute.ToString() == "<sip:82.195.148.216:5062;lr>", "The request route information was not correctly preprocessed.");
            }

            [Test]
            public void StrictRoutePriorToProxyUnitTest()
            {
                Console.WriteLine("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);

                string sipMsg =
                    "INVITE sip:82.195.148.216:5062;lr SIP/2.0" + m_CRLF +
                    "Via: SIP/2.0/UDP 192.168.1.2:5065;rport;branch=z9hG4bKFBB7EAC06934405182D13950BD51F001" + m_CRLF +
                    "Route: <sip:89.100.92.186:45270;lr>,<sip:303@sip.blueface.ie>" + m_CRLF +
                    "From: SER Test X <sip:aaronxten@sip.blueface.ie:5065>;tag=196468136" + m_CRLF +
                    "To: <sip:303@sip.blueface.ie>" + m_CRLF +
                    "Contact: <sip:aaronxten@192.168.1.2:5065>" + m_CRLF +
                    "Call-ID: A3DF9A04-0EFE-47E4-98B1-E18AA186F3D6@192.168.1.2" + m_CRLF +
                    "CSeq: 49429 INVITE" + m_CRLF +
                    "Max-Forwards: 70" + m_CRLF +
                    "Content-Type: application/sdp" + m_CRLF +
                    "User-Agent: X-PRO release 1103v" + m_CRLF +
                    m_CRLF;

                SIPMessage sipMessage = SIPMessage.ParseSIPMessage(Encoding.UTF8.GetBytes(sipMsg), null, null);
                SIPRequest inviteReq = SIPRequest.ParseSIPRequest(sipMessage);

                SIPTransport mockSIPTransport = new SIPTransport(MockSIPDNSManager.Resolve, null, false);
                mockSIPTransport.AddSIPChannel(new MockSIPChannel(IPSocket.ParseSocketString("82.195.148.216:5062")));

                mockSIPTransport.PreProcessRouteInfo(inviteReq);

                Console.WriteLine(inviteReq.ToString());
                Console.WriteLine("Next Route=" + inviteReq.Header.Routes.TopRoute.ToString());
                Console.WriteLine("Request URI=" + inviteReq.URI.ToString());

                Assert.IsTrue(inviteReq.Header.Routes.TopRoute.ToString() == "<sip:89.100.92.186:45270;lr>", "Top route was not correct.");
                Assert.IsTrue(inviteReq.URI.ToString() == "sip:303@sip.blueface.ie", "The request URI was incorrectly adjusted.");
                Assert.IsTrue(inviteReq.Header.Routes.Length == 1, "The route set was not correct.");
            }

            [Test]
            public void StrictRouteAfterProxyUnitTest()
            {
                Console.WriteLine("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);

                string sipMsg =
                    "INVITE sip:303@sip.blueface.ie SIP/2.0" + m_CRLF +
                    "Via: SIP/2.0/UDP 192.168.1.2:5065;rport;branch=z9hG4bKFBB7EAC06934405182D13950BD51F001" + m_CRLF +
                    "Route: <sip:82.195.148.216:5062;lr>,<sip:10.10.10.10>,<sip:89.100.92.186:45270;lr>" + m_CRLF +
                    "From: SER Test X <sip:aaronxten@sip.blueface.ie:5065>;tag=196468136" + m_CRLF +
                    "To: <sip:303@sip.blueface.ie>" + m_CRLF +
                    "Contact: <sip:aaronxten@192.168.1.2:5065>" + m_CRLF +
                    "Call-ID: A3DF9A04-0EFE-47E4-98B1-E18AA186F3D6@192.168.1.2" + m_CRLF +
                    "CSeq: 49429 INVITE" + m_CRLF +
                    "Max-Forwards: 70" + m_CRLF +
                    "Content-Type: application/sdp" + m_CRLF +
                    "User-Agent: X-PRO release 1103v" + m_CRLF +
                    m_CRLF;

                SIPMessage sipMessage = SIPMessage.ParseSIPMessage(Encoding.UTF8.GetBytes(sipMsg), null, null);
                SIPRequest inviteReq = SIPRequest.ParseSIPRequest(sipMessage);

                SIPTransport mockSIPTransport = new SIPTransport(MockSIPDNSManager.Resolve, null, false);
                mockSIPTransport.AddSIPChannel(new MockSIPChannel(IPSocket.ParseSocketString("82.195.148.216:5062")));

                mockSIPTransport.PreProcessRouteInfo(inviteReq);

                Console.WriteLine("Next Route=" + inviteReq.Header.Routes.TopRoute.ToString());
                Console.WriteLine("Request URI=" + inviteReq.URI.ToString());

                Assert.IsTrue(inviteReq.Header.Routes.TopRoute.ToString() == "<sip:89.100.92.186:45270;lr>", "Top route was not correctly recognised.");
                Assert.IsTrue(inviteReq.Header.Routes.BottomRoute.ToString() == "<sip:303@sip.blueface.ie>", "Bottom route was not correctly placed.");
                Assert.IsTrue(inviteReq.URI.ToString() == "sip:10.10.10.10", "The request URI was not correctly adjusted.");
                Assert.IsTrue(inviteReq.Header.Routes.Length == 2, "The route set was not correct.");
            }

            [Test]
            [Ignore("This SIP stack will not put hostnames into a Route header in order to avoid unnecessary DNS lookups.")]
            public void LooseRouteForProxyHostnameUnitTest()
            {
                Console.WriteLine("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);

                string sipMsg =
                    "INVITE sip:303@sip.blueface.ie SIP/2.0" + m_CRLF +
                    "Via: SIP/2.0/UDP 192.168.1.2:5065;rport;branch=z9hG4bKFBB7EAC06934405182D13950BD51F001" + m_CRLF +
                    "Route: <sip:sip.blueface.ie;lr>,<sip:89.100.92.186:45270;lr>" + m_CRLF +
                    "From: SER Test X <sip:aaronxten@sip.blueface.ie:5065>;tag=196468136" + m_CRLF +
                    "To: <sip:303@sip.blueface.ie>" + m_CRLF +
                    "Contact: <sip:aaronxten@192.168.1.2:5065>" + m_CRLF +
                    "Call-ID: A3DF9A04-0EFE-47E4-98B1-E18AA186F3D6@192.168.1.2" + m_CRLF +
                    "CSeq: 49429 INVITE" + m_CRLF +
                    "Max-Forwards: 70" + m_CRLF +
                    "Content-Type: application/sdp" + m_CRLF +
                    "User-Agent: X-PRO release 1103v" + m_CRLF +
                    m_CRLF;

                SIPMessage sipMessage = SIPMessage.ParseSIPMessage(Encoding.UTF8.GetBytes(sipMsg), null, null);
                SIPRequest inviteReq = SIPRequest.ParseSIPRequest(sipMessage);

                SIPTransport mockSIPTransport = new SIPTransport(MockSIPDNSManager.Resolve, null, false);
                mockSIPTransport.AddSIPChannel(new MockSIPChannel(IPSocket.ParseSocketString("194.213.29.100:5060")));

                mockSIPTransport.PreProcessRouteInfo(inviteReq);

                Assert.IsTrue(inviteReq.URI.ToString() == "sip:303@sip.blueface.ie", "The request URI was incorrectly modified.");
                Assert.IsTrue(inviteReq.Header.Routes.TopRoute.ToString() == "<sip:89.100.92.186:45270;lr>", "The request route information was not correctly preprocessed.");
                Assert.IsTrue(inviteReq.Header.Routes.Length == 1, "The route set was an incorrect length.");
            }

            [Test]
            [ExpectedException(typeof(SIPValidationException))]
            public void SpuriousStartCharsInResponseUnitTest()
            {
                Console.WriteLine("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);

                // This is an example of a malformed response recieved in the wild. It matches the bnf format for a request,
                // if the format of the SIP URI is not taken into account.
                string sipMsg =
                    "16394SIP/2.0 200 OK" + m_CRLF +
                    "To: <sip:user@83.70.216.94:5056>;tag=56314300b3ccd13fi0" + m_CRLF +
                    "From: <sip:natkeepalive@194.213.29.52:5064>;tag=7816855980" + m_CRLF +
                    "Call-ID: 1652975648@194.213.29.52" + m_CRLF +
                    "CSeq: 685 OPTIONS" + m_CRLF +
                    "Via: SIP/2.0/UDP 213.168.225.133:5060;branch=z9hG4bK46427189218ce28213adfb77e9df73b8ba6f6f0b" + m_CRLF +
                    "Via: SIP/2.0/UDP 194.213.29.52:5064;branch=z9hG4bK1531800555" + m_CRLF +
                    "Server: Linksys/PAP2-3.1.3(LS)" + m_CRLF +
                    "Content-Length: 5" + m_CRLF +
                    "Allow: ACK, BYE, CANCEL, INFO, INVITE, NOTIFY, OPTIONS, REFER" + m_CRLF +
                    "Supported: x-sipura" + m_CRLF + m_CRLF;

                SIPMessage sipMessage = SIPMessage.ParseSIPMessage(Encoding.UTF8.GetBytes(sipMsg), null, null);
                SIPRequest sipRequest = SIPRequest.ParseSIPRequest(sipMessage);

                Console.WriteLine("-----------------------------------------");
            }

            [Test]
            public void RegisterZeroExpiryUnitTest()
            {
                Console.WriteLine("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);

                string sipMsg =
                    "REGISTER sip:213.200.94.181 SIP/2.0" + m_CRLF +
                    "Via: SIP/2.0/UDP 192.168.1.32:10254;branch=z9hG4bK-d87543-eb7c9f44883c5955-1--d87543-;rport;received=89.100.104.191" + m_CRLF +
                    "To: aaronxten <sip:aaronxten@213.200.94.181>" + m_CRLF +
                    "From: aaronxten <sip:aaronxten@213.200.94.181>;tag=774d2561" + m_CRLF +
                    "Call-ID: MTBhNGZjZmQ2OTc3MWU5MTZjNWUxMDYxOTk1MjdmYzk." + m_CRLF +
                    "CSeq: 2 REGISTER" + m_CRLF +
                    "Contact: <sip:aaronxten@192.168.1.32:10254;rinstance=6d2bbd8014ca7a76>;expires=0" + m_CRLF +
                    "Max-Forwards: 69" + m_CRLF +
                    "User-Agent: X-Lite release 1006e stamp 34025" + m_CRLF +
                    "Allow: INVITE, ACK, CANCEL, OPTIONS, BYE, REFER, NOTIFY, MESSAGE, SUBSCRIBE, INFO" + m_CRLF + m_CRLF;

                SIPMessage sipMessage = SIPMessage.ParseSIPMessage(Encoding.UTF8.GetBytes(sipMsg), null, null);
                SIPRequest registerReq = SIPRequest.ParseSIPRequest(sipMessage);

                Console.WriteLine(registerReq.ToString());

                Console.WriteLine("-----------------------------------------");
            }

            [Test]
            public void AvayaInviteUnitTest()
            {
                Console.WriteLine("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);

                string sipMsg =
                    "INVITE sip:194.213.29.100:5060 SIP/2.0" + m_CRLF +
                    "Via: SIP/2.0/UDP 10.1.1.241;branch=z9hG4bK94fc63626" + m_CRLF +
                    "To: UNKNOWN <sip:8219000@sip.194.213.29.100>" + m_CRLF +
                    "From: 'Joe Bloggs' <sip:ei9gz@blueface.ie>;tag=cc16d34c122e5fe" + m_CRLF +
                    "Call-ID: 61d0b3a80f5727a9be56ac739e8b0581@blueface.ie" + m_CRLF +
                    "CSeq: 2009546910 INVITE" + m_CRLF +
                    "Contact: 'Val Gavin' <sip:ei9gz@10.1.1.241>" + m_CRLF +
                    "Max-Forwards: 70" + m_CRLF +
                    "Route: <sip:8219522@sip.194.213.29.100>" + m_CRLF +    // Strict Route header (this header is actually a fault but it ends up being a strict route).
                    "User-Agent: NeuralX MxSF/v3.2.6.26" + m_CRLF +
                    "Content-Type: application/sdp" + m_CRLF +
                    "Content-Length: 318" + m_CRLF +
                    "P-Asserted-Identity: 'Joe Bloggs' <sip:9@blueface.ie>" + m_CRLF +
                    "Allow: INVITE" + m_CRLF +
                    "Allow: CANCEL" + m_CRLF +
                    "Allow: OPTIONS" + m_CRLF +
                    "Allow: BYE" + m_CRLF +
                    "Allow: REFER" + m_CRLF +
                    "Allow: INFO" + m_CRLF +
                    "Allow: UPDATE" + m_CRLF +
                    "Supported: replaces" + m_CRLF +
                    m_CRLF +
                    "v=0" + m_CRLF +
                    "o=xxxxx 1174909600 1174909601 IN IP4 10.1.1.241" + m_CRLF +
                    "s=-" + m_CRLF +
                    "c=IN IP4 10.1.1.241" + m_CRLF +
                    "t=0 0" + m_CRLF +
                    "a=sendrecv" + m_CRLF +
                    "m=audio 20026 RTP/AVP 8 0 18 101" + m_CRLF +
                    "a=rtpmap:8 PCMA/8000" + m_CRLF +
                    "a=rtpmap:0 PCMU/8000" + m_CRLF +
                    "a=rtpmap:18 G729/8000" + m_CRLF +
                    "a=rtpmap:101 telephone-event/8000" + m_CRLF +
                    "a=fmtp:18 annexb=no" + m_CRLF +
                    "a=fmtp:101 0-15" + m_CRLF +
                    "a=ptime:20" + m_CRLF +
                    "a=rtcp:20027 IN IP4 10.1.1.241";

                SIPMessage sipMessage = SIPMessage.ParseSIPMessage(Encoding.UTF8.GetBytes(sipMsg), null, null);
                SIPRequest inviteReq = SIPRequest.ParseSIPRequest(sipMessage);

                SIPTransport mockSIPTransport = new SIPTransport(MockSIPDNSManager.Resolve, null, false);
                mockSIPTransport.AddSIPChannel(new MockSIPChannel(IPSocket.ParseSocketString("194.213.29.100:5060")));

                mockSIPTransport.PreProcessRouteInfo(inviteReq);

                Console.WriteLine(inviteReq.ToString());

                Assert.IsTrue(inviteReq.URI.ToString() == "sip:8219522@sip.194.213.29.100", "The request URI was not updated to the strict route.");
                Assert.IsTrue(inviteReq.Header.Routes.TopRoute.URI.ToString() == "sip:194.213.29.100:5060", "The route set was not correctly updated.");

                Console.WriteLine("-----------------------------------------");
            }

            [Test]
            public void LocalphoneInviteUnitTest()
            {
                Console.WriteLine("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);

                string sipMsg =
                    "INVITE sip:shebeen@sip.mysipswitch.com;switchtag=134308 SIP/2.0" + m_CRLF +
                    "Via: SIP/2.0/UDP 77.75.25.45;branch=z9hG4bK048.7b51ac95.0" + m_CRLF +
                    "Via: SIP/2.0/UDP 213.166.9.4:5060;branch=76c5c145958c18a58a4b8f83c82476d8;rport=5060" + m_CRLF +
                    "To: \"02031296073\" <sip:02031296073@213.166.9.4>" + m_CRLF +
                    "From: \"Anonymous\" <sip:213.166.9.4:5060>;tag=3433217327-893254" + m_CRLF +
                    "Call-ID: 334573-3433217327-893210@interface-e1000g0" + m_CRLF +
                    "CSeq: 1 INVITE" + m_CRLF +
                    "Contact: <sip:213.166.9.4:5060>" + m_CRLF +
                    "Max-Forwards: 12" + m_CRLF +
                    "Record-Route: <sip:77.75.25.45;lr=on;ftag=3433217327-893254>" + m_CRLF +
                    "Content-Type: application/sdp" + m_CRLF +
                    "Content-Length: 162" + m_CRLF +
                    "Session-Expires: 3600;Refresher=uac" + m_CRLF +
                    "Supported: timer" + m_CRLF +
                    "P-Asserted-Identity:<sip:unknown@213.166.9.4>" + m_CRLF +
                    "Privacy: none" + m_CRLF +
                    m_CRLF +
                    "v=0" + m_CRLF +
                    "o=NexTone-MSW 1234 0 IN IP4 213.166.9.6" + m_CRLF +
                    "s=sip call" + m_CRLF +
                    "c=IN IP4 213.166.9.6" + m_CRLF +
                    "t=0 0" + m_CRLF +
                    "m=audio 55694 RTP/AVP 0 8 18" + m_CRLF +
                    "a=rtpmap:18 G729/8000" + m_CRLF +
                    "a=fmtp:18 annexb=yes";

                SIPMessage sipMessage = SIPMessage.ParseSIPMessage(Encoding.UTF8.GetBytes(sipMsg), null, null);
                SIPRequest inviteReq = SIPRequest.ParseSIPRequest(sipMessage);

                Console.WriteLine(inviteReq.ToString());

                Console.WriteLine("-----------------------------------------");
            }

            [Test]
            public void MultipleRouteHeadersUnitTest()
            {
                Console.WriteLine("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);

                string sipMsg =
                    "BYE sip:bluesipd@192.168.1.2:5065 SIP/2.0" + m_CRLF +
                    "Via: SIP/2.0/UDP 213.168.225.133:5060;branch=z9hG4bK483ca249;rport" + m_CRLF +
                    "Route: <sip:220.240.255.198:64300;lr>,<sip:bluesipd@192.168.1.2:5065>" + m_CRLF +
                    "Route: <sip:21.10.21.2;lr>,<sip:bluesipd@90.91.82.12>,<sip:bluesipd@90.91.82.12>" + m_CRLF +
                    "Route: <sip:2.3.22.2;lr>,<sip:bluesipd@90.91.82.12>" + m_CRLF +
                    "From: <sip:303@bluesipd>;tag=as7a10c774" + m_CRLF +
                    "To: bluesipd <sip:bluesipd@bluesipd:5065>;tag=2561975990" + m_CRLF +
                    "Contact: <sip:303@213.168.225.133>" + m_CRLF +
                    "Call-ID: D9D08936-5455-476C-A5A2-A069D4B8DBFF@192.168.1.2" + m_CRLF +
                    "CSeq: 102 BYE" + m_CRLF +
                    "User-Agent: asterisk" + m_CRLF +
                    "Content-Length: 0" + m_CRLF + m_CRLF;

                SIPMessage sipMessage = SIPMessage.ParseSIPMessage(Encoding.UTF8.GetBytes(sipMsg), null, null);
                SIPRequest byeReq = SIPRequest.ParseSIPRequest(sipMessage);

                Assert.IsTrue(byeReq.Header.Routes.Length == 7, "The wrong number of Ruute headers were parsed.");
                SIPRoute nextRoute = byeReq.Header.Routes.PopRoute();
                Assert.IsTrue(nextRoute.Host == "220.240.255.198:64300", "The first route was incorrect.");
                nextRoute = byeReq.Header.Routes.PopRoute();
                Assert.IsTrue(nextRoute.Host == "192.168.1.2:5065", "The second route was incorrect.");
                nextRoute = byeReq.Header.Routes.PopRoute();
                Assert.IsTrue(nextRoute.Host == "21.10.21.2", "The third route was incorrect.");
                nextRoute = byeReq.Header.Routes.PopRoute();
                Assert.IsTrue(nextRoute.Host == "90.91.82.12", "The fourth route was incorrect.");
                nextRoute = byeReq.Header.Routes.PopRoute();
                Assert.IsTrue(nextRoute.Host == "90.91.82.12", "The fifth route was incorrect.");
                nextRoute = byeReq.Header.Routes.PopRoute();
                Assert.IsTrue(nextRoute.Host == "2.3.22.2", "The sixth route was incorrect.");
                nextRoute = byeReq.Header.Routes.PopRoute();
                Assert.IsTrue(nextRoute.Host == "90.91.82.12", "The seventh route was incorrect.");

                Console.WriteLine("-----------------------------------------");
            }
        }

		#endif

		#endregion
	}
}
