-- Drop tables
SET FOREIGN_KEY_CHECKS=0;
DROP TABLE IF EXISTS `plugins`;
DROP TABLE IF EXISTS `settings`;
DROP TABLE IF EXISTS `urlrewriting`;
DROP TABLE IF EXISTS `html_templates`;
SET FOREIGN_KEY_CHECKS=1;

-- Create tables
CREATE TABLE IF NOT EXISTS plugins
(
	pluginid INT PRIMARY KEY AUTO_INCREMENT,
	title TEXT,
	directory TEXT,
	classpath TEXT,
	cycle_interval INT DEFAULT 0,
	invoke_order INT DEFAULT 0,
	state INT DEFAULT 0,
	handles_404 VARCHAR(1) DEFAULT 0,
	handles_request_start VARCHAR(1) DEFAULT 0,
	handles_request_end VARCHAR(1) DEFAULT 0
);
CREATE TABLE IF NOT EXISTS settings2
(
	category VARCHAR(25),
	keyname VARCHAR(30),
	PRIMARY KEY(category, keyname),
	pluginid INT,
	FOREIGN KEY(`pluginid`) REFERENCES `plugins`(`pluginid`) ON UPDATE CASCADE ON DELETE CASCADE,
	value TEXT,
	description TEXT
);
CREATE TABLE IF NOT EXISTS urlrewriting
(
	urlid INT PRIMARY KEY AUTO_INCREMENT,
	parent INT,
	FOREIGN KEY(`parent`) REFERENCES `urlrewriting`(`urlid`) ON UPDATE CASCADE ON DELETE CASCADE,
	pluginid INT,
	FOREIGN KEY(`pluginid`) REFERENCES `plugins`(`pluginid`) ON UPDATE CASCADE ON DELETE CASCADE,
	title TEXT
);
CREATE TABLE IF NOT EXISTS html_templates
(
	pkey VARCHAR(25),
	hkey VARCHAR(25),
	description TEXT,
	html TEXT,
	PRIMARY KEY(pkey, hkey)
);
CREATE TABLE IF NOT EXISTS email_queue
(
	emailid INT PRIMARY KEY AUTO_INCREMENT,
	email TEXT,
	subject TEXT,
	body TEXT,
	html VARCHAR(1) DEFAULT 1
);