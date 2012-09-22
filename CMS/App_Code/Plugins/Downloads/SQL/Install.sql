CREATE TABLE IF NOT EXISTS downloads_folders
(
	folderid INT PRIMARY KEY AUTO_INCREMENT,
	title TINYTEXT NOT NULL,
	path TEXT NOT NULL,
	datetime DATETIME
);
CREATE TABLE IF NOT EXISTS downloads_files_icons
(
	iconid INT PRIMARY KEY AUTO_INCREMENT,
	hash VARCHAR(40) UNIQUE,
	data BLOB NOT NULL
);
CREATE TABLE IF NOT EXISTS downloads_files
(
	downloadid INT PRIMARY KEY AUTO_INCREMENT,
	folderid INT,
	FOREIGN KEY(`folderid`) REFERENCES `downloads_folders`(`folderid`) ON UPDATE CASCADE ON DELETE CASCADE,
	title TINYTEXT NOT NULL,
	extension TINYTEXT,
	physical_path TEXT NOT NULL,
	file_size BIGINT NOT NULL,
	description MEDIUMTEXT,
	iconid INT,
	FOREIGN KEY(`iconid`) REFERENCES `downloads_files_icons`(`iconid`) ON UPDATE CASCADE ON DELETE SET NULL,
	datetime DATETIME
);
CREATE TABLE IF NOT EXISTS downloads
(
	downloadid INT,
	FOREIGN KEY(`downloadid`) REFERENCES `downloads_files`(`downloadid`) ON UPDATE CASCADE ON DELETE SET NULL,
	-- 39 length due to maximum ipv6 length
	ip_addr VARCHAR(45) NOT NULL,
	datetime DATETIME
);
CREATE TABLE IF NOT EXISTS downloads_ext_icons
(
	extension VARCHAR(10) PRIMARY KEY,
	icon BLOB
);