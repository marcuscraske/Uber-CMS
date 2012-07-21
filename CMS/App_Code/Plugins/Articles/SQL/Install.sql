CREATE TABLE IF NOT EXISTS articles
(
	articleid INT PRIMARY KEY AUTO_INCREMENT,
	title TEXT,
	relative_url TEXT,
	userid INT,
	FOREIGN KEY(`userid`) REFERENCES `bsa_users`(`userid`) ON UPDATE CASCADE ON DELETE SET NULL,
	body TEXT,
	published BOOL DEFAULT 0,
	allow_comments BOOL DEFAULT 0,
	moderator_userid INT,
	FOREIGN KEY(`moderator_userid`) REFERENCES `bsa_users`(`userid`) ON UPDATE CASCADE ON DELETE SET NULL
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