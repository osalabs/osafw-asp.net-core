ALTER TABLE users ADD
  mfa_secret            NVARCHAR(32), -- mfa secret code, if empty - no mfa for the user configured
  mfa_recovery          NVARCHAR(255), -- mfa recovery hashed codes, comma-separated
  mfa_added             DATETIME2    -- last datetime when mfa setup or resynced
;
