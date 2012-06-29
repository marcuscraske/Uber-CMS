-- Create tables
CREATE TABLE IF NOT EXISTS `bsa_user_groups`
(
	groupid INT PRIMARY KEY AUTO_INCREMENT,
	title TEXT NOT NULL,
	access_login VARCHAR(1) DEFAULT 1,
	access_changeaccount VARCHAR(1) DEFAULT 1,
	access_media_create VARCHAR(1) DEFAULT 1,
	access_media_edit VARCHAR(1) DEFAULT 0,
	access_media_delete VARCHAR(1) DEFAULT 0,
	access_media_publish VARCHAR(1) DEFAULT 0,
	access_admin VARCHAR(1) DEFAULT 0
);
CREATE TABLE IF NOT EXISTS `bsa_users`
(
	userid INT PRIMARY KEY AUTO_INCREMENT,
	groupid INT,
	FOREIGN KEY(`groupid`) REFERENCES `bsa_user_groups`(`groupid`) ON UPDATE CASCADE ON DELETE CASCADE,
	username TEXT NOT NULL,
	password TEXT NOT NULL,
	email TEXT NOT NULL,
	secret_question TEXT,
	secret_answer TEXT
);
CREATE TABLE IF NOT EXISTS `bsa_user_log`
(
	logid INT PRIMARY KEY AUTO_INCREMENT,
	userid INT NOT NULL,
	FOREIGN KEY(`userid`) REFERENCES `bsa_users`(`userid`) ON UPDATE CASCADE ON DELETE CASCADE,
	event_type INT,
	date TIMESTAMP,
	additional_info TEXT
);
CREATE TABLE IF NOT EXISTS `bsa_failed_logins`
(
	ip TEXT,
	attempted_username TEXT,
	datetime TIMESTAMP
);