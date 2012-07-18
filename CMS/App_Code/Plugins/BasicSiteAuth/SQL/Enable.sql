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
CREATE TABLE IF NOT EXISTS `bsa_user_groups_labels`
(
	labelid INT PRIMARY KEY AUTO_INCREMENT,
	column_title TEXT NOT NULL,
	title TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS `bsa_users`
(
	userid INT PRIMARY KEY AUTO_INCREMENT,
	groupid INT,
	FOREIGN KEY(`groupid`) REFERENCES `bsa_user_groups`(`groupid`) ON UPDATE CASCADE ON DELETE CASCADE,
	username VARCHAR(18) NOT NULL,
	UNIQUE(username),
	password TEXT NOT NULL,
	email VARCHAR(50) NOT NULL,
	UNIQUE(email),
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
CREATE TABLE IF NOT EXISTS `bsa_user_bans`
(
	banid INT PRIMARY KEY AUTO_INCREMENT,
	userid INT NOT NULL,
	FOREIGN KEY(`userid`) REFERENCES `bsa_users`(`userid`) ON UPDATE CASCADE ON DELETE CASCADE,
	reason TEXT,
	unban_date DATETIME,
	datetime DATETIME,
	banner_userid INT,
	FOREIGN KEY(`banner_userid`) REFERENCES `bsa_users`(`userid`) ON UPDATE CASCADE ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS `bsa_failed_logins`
(
	ip TEXT,
	attempted_username TEXT,
	datetime TIMESTAMP
);
CREATE TABLE IF NOT EXISTS `bsa_activations`
(
	keyid INT PRIMARY KEY AUTO_INCREMENT,
	userid INT,
	FOREIGN KEY(`userid`) REFERENCES `bsa_users`(`userid`) ON UPDATE CASCADE ON DELETE CASCADE,
	code VARCHAR(16) NOT NULL,
	UNIQUE(code)
);
CREATE TABLE IF NOT EXISTS `bsa_recovery_email`
(
	recoveryid INT PRIMARY KEY AUTO_INCREMENT,
	userid INT,
	FOREIGN KEY(`userid`) REFERENCES `bsa_users`(`userid`) ON UPDATE CASCADE ON DELETE CASCADE,
	code VARCHAR(16) NOT NULL,
	UNIQUE(code),
	ip TEXT,
	datetime_dispatched TIMESTAMP
);
CREATE TABLE IF NOT EXISTS `bsa_recovery_sqa_attempts`
(
	attemptid INT PRIMARY KEY AUTO_INCREMENT,
	ip TEXT,
	datetime TIMESTAMP
);
CREATE TABLE IF NOT EXISTS `bsa_admin_pages`
(
	pageid INT PRIMARY KEY AUTO_INCREMENT,
	classpath TEXT,
	method TEXT,
	title TEXT,
	category TEXT,
	menu_icon TEXT
);
-- Insert user group permission columns
INSERT INTO bsa_user_groups_labels (column_title, title)
VALUES
	('access_login', 'Login'),
	('access_changeaccount', 'Change Account Details'),
	('access_media_create', 'Media - Create'),
	('access_media_edit', 'Media - Edit'),
	('access_media_delete', 'Media - Delete'),
	('access_media_publish', 'Media - Publish'),
	('access_admin', 'Admin Permissions')
;