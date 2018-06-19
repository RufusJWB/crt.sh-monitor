// <copyright file="Certificate.cs" company="Siemens AG">
// Copyright (c) Siemens AG. All rights reserved.
// Licensed under the GPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Monitor.DAL
{
    using System;
    using System.Globalization;
    using System.Text;

    /// <summary>
    /// Represents a single certificate.
    /// </summary>
    public class Certificate
    {
        /// <summary>
        /// Gets or sets the ID of this certificate.
        /// </summary>
        public string CertificateID { get; set; }

#pragma warning disable CA1819 // Properties should not return arrays
        /// <summary>
        /// Gets or sets the serial number of this certificate.
        /// </summary>
        public byte[] SerialNumber { get; set; }
#pragma warning restore CA1819 // Properties should not return arrays

        /// <summary>
        /// Gets or sets the SDN of this certificate.
        /// </summary>
        public string SubjectDistinguishedName { get; set; }

        /// <summary>
        /// Gets or sets the type of this certificate.
        /// </summary>
        public string CertificateType { get; set; }

        /// <summary>
        /// Gets or sets the not before date of this certificate.
        /// </summary>
        public DateTime NotBefore { get; set; }

        /// <summary>
        /// Gets or sets the not after date of this certificate.
        /// </summary>
        public DateTime NotAfter { get; set; }

        /// <summary>
        /// Gets or sets the first seen date of this certificate.
        /// </summary>
        public DateTime FirstSeen { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this certificate is revoked.
        /// </summary>
        public bool Revoked { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this certificate is expired.
        /// </summary>
        public bool Expired { get; set; }

        /// <summary>
        /// Gets or sets the ammount of errors found by linters.
        /// </summary>
        public int LintErrors { get; set; }

        /// <summary>
        /// Gets a link to this certificate in crt.sh.
        /// </summary>
        public string CrtSHLink
        {
            get
            {
                return $"https://crt.sh/?id={this.CertificateID}&opt=cablint,x509lint,zlint";
            }
        }

        /// <summary>
        /// Gets the hexadecimal represention of the serial number.
        /// </summary>
        public string HexSerialNumber
        {
            get
            {
                return BitConverter.ToString(this.SerialNumber);
            }
        }
    }
}
