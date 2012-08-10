SET FOREIGN_KEY_CHECKS=0;
CREATE TABLE IF NOT EXISTS articles_thread
(
	threadid INT PRIMARY KEY AUTO_INCREMENT,
	relative_url VARCHAR(32) NOT NULL UNIQUE,
	articleid_current INT,
	FOREIGN KEY(`articleid_current`) REFERENCES `articles`(`articleid`) ON UPDATE CASCADE ON DELETE SET NULL
);
SET FOREIGN_KEY_CHECKS=1;
CREATE TABLE IF NOT EXISTS articles
(
	articleid INT PRIMARY KEY AUTO_INCREMENT,
	threadid INT,
	FOREIGN KEY(`threadid`) REFERENCES `articles_thread`(`threadid`) ON UPDATE CASCADE ON DELETE CASCADE,
	title TEXT,
	userid INT,
	FOREIGN KEY(`userid`) REFERENCES `bsa_users`(`userid`) ON UPDATE CASCADE ON DELETE SET NULL,
	body TEXT,
	moderator_userid INT,
	FOREIGN KEY(`moderator_userid`) REFERENCES `bsa_users`(`userid`) ON UPDATE CASCADE ON DELETE SET NULL,
	published VARCHAR(1) DEFAULT 0,
	allow_comments VARCHAR(1) DEFAULT 0,
	allow_html VARCHAR(1) DEFAULT 0,
	show_pane VARCHAR(1) DEFAULT 0,
	datetime DATETIME
);
CREATE TABLE IF NOT EXISTS articles_tags
(
	tagid INT PRIMARY KEY AUTO_INCREMENT,
	keyword VARCHAR(20) NOT NULL UNIQUE
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