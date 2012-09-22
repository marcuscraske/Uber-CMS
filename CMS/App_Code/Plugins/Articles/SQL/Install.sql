SET FOREIGN_KEY_CHECKS=0;
CREATE TABLE IF NOT EXISTS articles_thread
(
	threadid INT PRIMARY KEY AUTO_INCREMENT,
	relative_url VARCHAR(32) NOT NULL UNIQUE,
	articleid_current INT,
	FOREIGN KEY(`articleid_current`) REFERENCES `articles`(`articleid`) ON UPDATE CASCADE ON DELETE SET NULL
);
CREATE TABLE IF NOT EXISTS articles
(
	articleid INT PRIMARY KEY AUTO_INCREMENT,
	threadid INT,
	FOREIGN KEY(`threadid`) REFERENCES `articles_thread`(`threadid`) ON UPDATE CASCADE ON DELETE CASCADE,
	title TEXT,
	userid INT,
	FOREIGN KEY(`userid`) REFERENCES `bsa_users`(`userid`) ON UPDATE CASCADE ON DELETE SET NULL,
	thumbnailid INT,
	FOREIGN KEY(`thumbnailid`) REFERENCES `articles_thumbnails`(`thumbnailid`) ON UPDATE CASCADE ON DELETE SET NULL,
	body TEXT,
	body_cached TEXT,
	moderator_userid INT,
	FOREIGN KEY(`moderator_userid`) REFERENCES `bsa_users`(`userid`) ON UPDATE CASCADE ON DELETE SET NULL,
	published VARCHAR(1) DEFAULT 0,
	allow_comments VARCHAR(1) DEFAULT 0,
	allow_html VARCHAR(1) DEFAULT 0,
	show_pane VARCHAR(1) DEFAULT 0,
	datetime DATETIME
);
-- Upgrade v1.2
CREATE PROCEDURE articles_upgrade_12()
BEGIN
	DECLARE CONTINUE HANDLER FOR 1060 BEGIN END;
	ALTER TABLE articles ADD body_cached MEDIUMTEXT;
END;
CALL articles_upgrade_12();
DROP  PROCEDURE articles_upgrade_12;
-- Change encoding support for utf8
ALTER TABLE `articles` CONVERT TO CHARACTER SET utf8 COLLATE utf8_general_ci;
SET FOREIGN_KEY_CHECKS=1;
CREATE TABLE IF NOT EXISTS articles_thumbnails
(
	thumbnailid INT PRIMARY KEY AUTO_INCREMENT,
	data BLOB
);
CREATE TABLE IF NOT EXISTS articles_tags
(
	tagid INT PRIMARY KEY AUTO_INCREMENT,
	keyword VARCHAR(30) NOT NULL UNIQUE
);
CREATE TABLE IF NOT EXISTS articles_tags_article
(
	tagid INT,
	articleid INT,
	FOREIGN KEY(`tagid`) REFERENCES `articles_tags`(`tagid`) ON UPDATE CASCADE ON DELETE CASCADE,
	FOREIGN KEY(`articleid`) REFERENCES `articles`(`articleid`) ON UPDATE CASCADE ON DELETE CASCADE,
	PRIMARY KEY(tagid, articleid)
);
CREATE TABLE IF NOT EXISTS articles_thread_comments
(
	commentid INT PRIMARY KEY AUTO_INCREMENT,
	threadid INT,
	FOREIGN KEY(`threadid`) REFERENCES `articles_thread`(`threadid`) ON UPDATE CASCADE ON DELETE CASCADE,
	userid INT,
	FOREIGN KEY(`userid`) REFERENCES `bsa_users`(`userid`) ON UPDATE CASCADE ON DELETE CASCADE,
	message TEXT,
	datetime DATETIME
);
CREATE TABLE IF NOT EXISTS articles_log_events
(
	event_type INT,
	userid INT,
	FOREIGN KEY(`userid`) REFERENCES `bsa_users`(`userid`) ON UPDATE CASCADE ON DELETE SET NULL,
	datetime DATETIME,
	articleid INT,
	threadid INT
);
CREATE TABLE IF NOT EXISTS articles_images
(
	imageid INT PRIMARY KEY AUTO_INCREMENT,
	title VARCHAR(25),
	userid INT,
	FOREIGN KEY(`userid`) REFERENCES `bsa_users`(`userid`) ON UPDATE CASCADE ON DELETE CASCADE,
	data MEDIUMBLOB,
	datetime DATETIME
);
CREATE TABLE IF NOT EXISTS articles_images_links
(
	articleid INT,
	FOREIGN KEY(`articleid`) REFERENCES `articles`(`articleid`) ON UPDATE CASCADE ON DELETE CASCADE,
	imageid INT,
	FOREIGN KEY(`imageid`) REFERENCES `articles_images`(`imageid`) ON UPDATE CASCADE ON DELETE CASCADE,
	PRIMARY KEY(articleid, imageid)
);
CREATE TABLE IF NOT EXISTS articles_format_providers
(
	classpath VARCHAR(30) NOT NULL,
	method VARCHAR(30) NOT NULL,
	pluginid INT,
	FOREIGN KEY(`pluginid`) REFERENCES `plugins`(`pluginid`) ON UPDATE CASCADE ON DELETE CASCADE,
	UNIQUE INDEX(classpath, method)
);