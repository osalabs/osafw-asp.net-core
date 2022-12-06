CREATE TABLE users_cookies (
    cookie_id           NVARCHAR(32) PRIMARY KEY CLUSTERED NOT NULL,      /*cookie id: time(secs)+rand(16)*/
    users_id            INT NOT NULL CONSTRAINT FK_users_cookies_users FOREIGN KEY REFERENCES users(id),

    add_time            DATETIME2 NOT NULL DEFAULT getdate()
);