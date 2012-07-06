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
CREATE TABLE IF NOT EXISTS `bsa_user_bans`
(
	banid INT PRIMARY KEY AUTO_INCREMENT,
	userid INT NOT NULL,
	FOREIGN KEY(`userid`) REFERENCES `bsa_users`(`userid`) ON UPDATE CASCADE ON DELETE CASCADE,
	reason TEXT,
	unban_date DATETIME,
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
	code TEXT
);
CREATE TABLE IF NOT EXISTS `bsa_recovery_email`
(
	recoveryid INT PRIMARY KEY AUTO_INCREMENT,
	userid INT,
	FOREIGN KEY(`userid`) REFERENCES `bsa_users`(`userid`) ON UPDATE CASCADE ON DELETE CASCADE,
	code TEXT,
	ip TEXT,
	datetime_dispatched TIMESTAMP
);
CREATE TABLE IF NOT EXISTS `bsa_recovery_sqa_attempts`
(
	attemptid INT PRIMARY KEY AUTO_INCREMENT,
	ip TEXT,
	datetime TIMESTAMP
);
-- Create procedures
CREATE PROCEDURE bsa_register
(
	IN groupid INT,
	IN username TEXT,
	IN password TEXT,
	IN email TEXT,
	IN secret_question TEXT,
	IN secret_answer TEXT,
	OUT userid INT
)
BEGIN
	DECLARE result int;
	IF (SELECT COUNT('') FROM bsa_users AS u WHERE u.email LIKE email) > 0 THEN
		SELECT -100 INTO result;
		ROLLBACK;
	ELSEIF (SELECT COUNT('') FROM bsa_users AS u WHERE u.username LIKE username) > 0 THEN
		SELECT -200 INTO result;
		ROLLBACK;
	ELSE
		INSERT INTO bsa_users (groupid, username, password, email, secret_question, secret_answer)
		VALUES
		(groupid, username, password, email, secret_question, secret_answer);
		SELECT LAST_INSERT_ID() INTO result;
		COMMIT;
	END IF;
	SELECT result INTO userid;
END;

CREATE PROCEDURE bsa_change_email
(
	IN userid INT,
	IN email TEXT,
	OUT errorcode INT
)
BEGIN
	DECLARE result int;
	IF (SELECT COUNT('') FROM bsa_users AS u WHERE u.email LIKE email AND u.userid != userid) > 0 THEN
		SELECT 0 INTO result;
		ROLLBACK;
	ELSE
		UPDATE bsa_users SET email=email WHERE userid=userid;
		SELECT 1 INTO result;
		COMMIT;
	END IF;
	SELECT result INTO errorcode;
END;