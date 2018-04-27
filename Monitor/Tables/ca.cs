using System;

namespace Monitor.Tables
{
    public class CA
    {
        long Id { get; set; } // ID serial,

        string Name { get; set; } // NAME text        NOT NULL,

        Byte[] PublicKey { get; set; } // PUBLIC_KEY bytea       NOT NULL,
        string Brand { get; set; } // BRAND text,

        Boolean LintingApplies { get; set; } // LINTING_APPLIES boolean     DEFAULT TRUE,

        long NoOfCertsIssued { get; set; } // NO_OF_CERTS_ISSUED bigint      DEFAULT 0	NOT NULL,
    }
}
