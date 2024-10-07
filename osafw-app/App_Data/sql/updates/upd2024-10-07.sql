-- expand field for hashed value storage

ALTER TABLE users_cookies
ALTER COLUMN cookie_id NVARCHAR(255) NOT NULL;