// <copyright file="ReturnValues.cs" company="Siemens AG">
// Copyright (c) Siemens AG. All rights reserved.
// Licensed under the GPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Monitor
{
    using System.Collections.Generic;

    /// <summary>
    /// Holding the returned certificates and some query paramaters.
    /// </summary>
    internal class ReturnValues
    {
        /// <summary>
        /// Gets or sets the ID of the CA.
        /// </summary>
        public long CAID { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether expired certificates shall be excluded.
        /// </summary>
        public bool ExcludeExpired { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether only certificates with LINT errors shall be included.
        /// </summary>
        public bool OnlyLINTErrors { get; set; }

        /// <summary>
        /// Gets or sets the maximum age of included certificates.
        /// </summary>
        public int DaysToLookBack { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether revoked certifcates shall be excluded.
        /// </summary>
        public bool ExcludeRevoked { get; set; }

        /// <summary>
        /// Gets or sets the list of all selected certificates.
        /// </summary>
        public IEnumerable<DAL.Certificate> Results { get; set; }
    }
}
