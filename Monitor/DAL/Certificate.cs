using System;
using System.Collections.Generic;
using System.Text;

namespace Monitor.DAL
{
    public class Certificate
    {
        string certificateID { get; set; }
        Byte[] serialNumber { get; set; }
        string subjectDistinguishedName { get; set; }
        DateTime notBefore { get; set; }
        DateTime notAfter { get; set; }
        DateTime firstSeen { get; set; }
        bool revoked { get; set; }
        int lintErrors { get; set; }

        string crtSHLink
        {
            get
            {
                return $"https://crt.sh/?id={certificateID}&opt=cablint,x509lint,zlint";
            }
        }
    }
}
