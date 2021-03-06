using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Globalization;
using System.Security.Authentication;
using System.Net;
using FluentFTP.Proxy;
#if !CORE
using System.Web;
#endif
#if (CORE || NETFX)
using System.Threading;

#endif
#if (CORE || NET45)
using System.Threading.Tasks;

#endif

namespace FluentFTP {
	public partial class FtpClient : IDisposable {
		#region Verification

		private bool VerifyTransfer(string localPath, string remotePath) {
			// verify args
			if (localPath.IsBlank()) {
				throw new ArgumentException("Required parameter is null or blank.", "localPath");
			}

			if (remotePath.IsBlank()) {
				throw new ArgumentException("Required parameter is null or blank.", "remotePath");
			}

			if (HasFeature(FtpCapability.HASH) || HasFeature(FtpCapability.MD5) ||
				HasFeature(FtpCapability.XMD5) || HasFeature(FtpCapability.XCRC) ||
				HasFeature(FtpCapability.XSHA1) || HasFeature(FtpCapability.XSHA256) ||
				HasFeature(FtpCapability.XSHA512)) {
				var hash = GetChecksum(remotePath);
				if (!hash.IsValid) {
					return false;
				}

				return hash.Verify(localPath);
			}

			//Not supported return true to ignore validation
			return true;
		}

		private bool VerifyFXPTransfer(string sourcePath, FtpClient fxpDestinationClient, string remotePath)
		{
			// verify args
			if (sourcePath.IsBlank())
			{
				throw new ArgumentException("Required parameter is null or blank.", "localPath");
			}

			if (remotePath.IsBlank())
			{
				throw new ArgumentException("Required parameter is null or blank.", "remotePath");
			}

			if (fxpDestinationClient is null)
			{
				throw new ArgumentNullException("Destination FXP FtpClient cannot be null!", "fxpDestinationClient");
			}

			if ((HasFeature(FtpCapability.HASH) && fxpDestinationClient.HasFeature(FtpCapability.HASH)) || (HasFeature(FtpCapability.MD5) && fxpDestinationClient.HasFeature(FtpCapability.MD5)) ||
				(HasFeature(FtpCapability.XMD5) && fxpDestinationClient.HasFeature(FtpCapability.XMD5)) || (HasFeature(FtpCapability.XCRC) && fxpDestinationClient.HasFeature(FtpCapability.XCRC)) ||
				(HasFeature(FtpCapability.XSHA1) && fxpDestinationClient.HasFeature(FtpCapability.XSHA1)) || (HasFeature(FtpCapability.XSHA256) & fxpDestinationClient.HasFeature(FtpCapability.XSHA256)) ||
				(HasFeature(FtpCapability.XSHA512) && fxpDestinationClient.HasFeature(FtpCapability.XSHA512)))
			{

				FtpHash sourceHash = GetChecksum(sourcePath);
				if (!sourceHash.IsValid)
				{
					return false;
				}


				FtpHash destinationHash = fxpDestinationClient.GetChecksum(remotePath);
				if (!destinationHash.IsValid)
				{
					return false;
				}

				return sourceHash.Value == destinationHash.Value;
			}
			else
			{
				LogLine(FtpTraceLevel.Info, "Source and Destination does not support the same hash algorythm");
			}

			//Not supported return true to ignore validation
			return true;
		}

#if ASYNC
		private async Task<bool> VerifyTransferAsync(string localPath, string remotePath, CancellationToken token = default(CancellationToken)) {
			// verify args
			if (localPath.IsBlank()) {
				throw new ArgumentException("Required parameter is null or blank.", "localPath");
			}

			if (remotePath.IsBlank()) {
				throw new ArgumentException("Required parameter is null or blank.", "remotePath");
			}

			if (HasFeature(FtpCapability.HASH) || HasFeature(FtpCapability.MD5) ||
				HasFeature(FtpCapability.XMD5) || HasFeature(FtpCapability.XCRC) ||
				HasFeature(FtpCapability.XSHA1) || HasFeature(FtpCapability.XSHA256) ||
				HasFeature(FtpCapability.XSHA512)) {
				FtpHash hash = await GetChecksumAsync(remotePath, token);
				if (!hash.IsValid) {
					return false;
				}

				return hash.Verify(localPath);
			}

			//Not supported return true to ignore validation
			return true;
		}

		private async Task<bool> VerifyFXPTransferAsync(string sourcePath, FtpClient fxpDestinationClient, string remotePath, CancellationToken token = default(CancellationToken))
		{
			// verify args
			if (sourcePath.IsBlank())
			{
				throw new ArgumentException("Required parameter is null or blank.", "localPath");
			}

			if (remotePath.IsBlank())
			{
				throw new ArgumentException("Required parameter is null or blank.", "remotePath");
			}

			if (fxpDestinationClient is null)
			{
				throw new ArgumentNullException("Destination FXP FtpClient cannot be null!", "fxpDestinationClient");
			}

			if ((HasFeature(FtpCapability.HASH) && fxpDestinationClient.HasFeature(FtpCapability.HASH)) || (HasFeature(FtpCapability.MD5) && fxpDestinationClient.HasFeature(FtpCapability.MD5)) ||
				(HasFeature(FtpCapability.XMD5) && fxpDestinationClient.HasFeature(FtpCapability.XMD5)) || (HasFeature(FtpCapability.XCRC) && fxpDestinationClient.HasFeature(FtpCapability.XCRC)) ||
				(HasFeature(FtpCapability.XSHA1) && fxpDestinationClient.HasFeature(FtpCapability.XSHA1)) || (HasFeature(FtpCapability.XSHA256) & fxpDestinationClient.HasFeature(FtpCapability.XSHA256)) ||
				(HasFeature(FtpCapability.XSHA512) && fxpDestinationClient.HasFeature(FtpCapability.XSHA512)))
			{
	
				FtpHash sourceHash = await GetChecksumAsync(sourcePath, token);
				if (!sourceHash.IsValid)
				{
					return false;
				}


				FtpHash destinationHash = await fxpDestinationClient.GetChecksumAsync(remotePath, token);
				if (!destinationHash.IsValid)
				{
					return false;
				}

				return sourceHash.Value == destinationHash.Value;
			}
			else
			{
				LogLine(FtpTraceLevel.Info, "Source and Destination does not support the same hash algorythm");
			}

			//Not supported return true to ignore validation
			return true;
		}

#endif

		#endregion

		#region Utilities

		/// <summary>
		/// Sends progress to the user, either a value between 0-100 indicating percentage complete, or -1 for indeterminate.
		/// </summary>
		private void ReportProgress(IProgress<FtpProgress> progress, long fileSize, long position, long bytesProcessed, TimeSpan elapsedtime, string localPath, string remotePath, FtpProgress metaProgress) {

			//  calculate % done, transfer speed and time remaining
			FtpProgress status = FtpProgress.Generate(fileSize, position, bytesProcessed, elapsedtime, localPath, remotePath, metaProgress);

			// send progress to parent
			progress.Report(status);
		}

		/// <summary>
		/// Sends progress to the user, either a value between 0-100 indicating percentage complete, or -1 for indeterminate.
		/// </summary>
		private void ReportProgress(Action<FtpProgress> progress, long fileSize, long position, long bytesProcessed, TimeSpan elapsedtime, string localPath, string remotePath, FtpProgress metaProgress) {

			//  calculate % done, transfer speed and time remaining
			FtpProgress status = FtpProgress.Generate(fileSize, position, bytesProcessed, elapsedtime, localPath, remotePath, metaProgress);

			// send progress to parent
			progress(status);
		}

		#endregion
	}
}