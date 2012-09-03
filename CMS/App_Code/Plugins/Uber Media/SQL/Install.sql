-- Version 1.0.0

-- Drop tables **************************************************************************************************************************************************************************
SET FOREIGN_KEY_CHECKS=0;
-- DROP TABLE IF EXISTS `um_external_requests`;
-- DROP TABLE IF EXISTS `um_item_types`;
-- DROP TABLE IF EXISTS `um_physical_folders`;
-- DROP TABLE IF EXISTS `um_physical_folder_types`;
-- DROP TABLE IF EXISTS `um_tags`;
-- DROP TABLE IF EXISTS `um_tag_items`;
-- DROP TABLE IF EXISTS `um_terminals`;
   DROP TABLE IF EXISTS `um_terminal_buffer`;
-- DROP TABLE IF EXISTS `um_virtual_items`;
-- DROP TABLE IF EXISTS `um_film_information`;
-- DROP TABLE IF EXISTS `um_film_information_providers`;
SET FOREIGN_KEY_CHECKS=1;

-- Create tables ************************************************************************************************************************************************************************
CREATE TABLE IF NOT EXISTS `um_external_requests`
(
	`reason` text,
	`url` text,
	`datetime` datetime DEFAULT NULL
);
CREATE TABLE IF NOT EXISTS `um_item_types`
(
	`typeid` INTEGER PRIMARY KEY AUTO_INCREMENT,
	`title` text,
	`uid` int DEFAULT NULL,
	`extensions` text,
	`thumbnail` text,
	`interface` text,
	`system` int(1) NOT NULL DEFAULT '0'
);
CREATE TABLE IF NOT EXISTS `um_physical_folders`
(
	`pfolderid` INTEGER PRIMARY KEY AUTO_INCREMENT,
	`title` text,
	`physicalpath` text NOT NULL,
	`allow_web_synopsis` int(1) NOT NULL
);
-- Physical folder types - joining physical folders and item_types
CREATE TABLE IF NOT EXISTS `um_physical_folder_types`
(
	`pfolderid` INT NOT NULL,
	`typeid` INT NOT NULL,
	FOREIGN KEY(`pfolderid`) REFERENCES `um_physical_folders`(`pfolderid`) ON DELETE CASCADE ON UPDATE CASCADE,
	FOREIGN KEY(`typeid`) REFERENCES `um_item_types`(`typeid`) ON DELETE CASCADE ON UPDATE CASCADE,
	PRIMARY KEY (`pfolderid`, `typeid`)
);
CREATE TABLE IF NOT EXISTS `um_tags`
(
	`tagid` INTEGER PRIMARY KEY AUTO_INCREMENT,
	`title` text
);
CREATE TABLE IF NOT EXISTS `um_virtual_items`
(
	`vitemid` INTEGER PRIMARY KEY AUTO_INCREMENT,
	`pfolderid` INT NOT NULL,
	FOREIGN KEY(`pfolderid`) REFERENCES `um_physical_folders`(`pfolderid`) ON DELETE CASCADE ON UPDATE CASCADE,
	`parent` int DEFAULT NULL,
	FOREIGN KEY(`parent`) REFERENCES `um_virtual_items`(`vitemid`) ON DELETE CASCADE ON UPDATE CASCADE,
	`type_uid` int(1) DEFAULT NULL,
	`title` text,
	`cache_rating` int NOT NULL DEFAULT '0',
	`description` text,
	`phy_path` text,
	`views` INT NOT NULL DEFAULT '0',
	`date_added` text,
	`thumbnail_data` blob
);
CREATE TABLE IF NOT EXISTS `um_tag_items`
(
	`tagid` INT NOT NULL,
	`vitemid` INT NOT NULL,
	FOREIGN KEY(`tagid`) REFERENCES `um_tags`(`tagid`) ON DELETE CASCADE ON UPDATE CASCADE,
	FOREIGN KEY(`vitemid`) REFERENCES `um_virtual_items`(`vitemid`) ON DELETE CASCADE ON UPDATE CASCADE,
	PRIMARY KEY (`tagid`,`vitemid`)
);
CREATE TABLE IF NOT EXISTS `um_terminals`
(
	`terminalid` INTEGER PRIMARY KEY AUTO_INCREMENT,
	`title` text,
	`status_state` text,
	`status_volume` double DEFAULT NULL,
	`status_volume_muted` int(1) DEFAULT NULL,
	`status_vitemid` int DEFAULT NULL,
	`status_position` int DEFAULT NULL,
	`status_duration` int DEFAULT NULL,
	`status_updated` datetime DEFAULT NULL
);
CREATE TABLE IF NOT EXISTS `um_terminal_buffer`
(
	`cid` BIGINT PRIMARY KEY AUTO_INCREMENT,
	`command` text,
	`terminalid` int DEFAULT NULL,
	FOREIGN KEY(`terminalid`) REFERENCES `um_terminals`(`terminalid`) ON DELETE CASCADE ON UPDATE CASCADE,
	`arguments` text,
	`queue` int DEFAULT '0'
);
CREATE TABLE IF NOT EXISTS `um_film_information_providers`
(
	`provid` integer PRIMARY KEY AUTO_INCREMENT,
	`title` text,
	`cache_updated` datetime
);
CREATE TABLE IF NOT EXISTS `um_film_information`
(
	`infoid` bigint PRIMARY KEY AUTO_INCREMENT,
	`title` text,
	`description` text,
	`provid` int,
	`last_updated` datetime,
	FOREIGN KEY(`provid`) REFERENCES `um_film_information_providers`(`provid`) ON DELETE CASCADE ON UPDATE CASCADE
);

-- Populate default data ****************************************************************************************************************************************************************
INSERT IGNORE INTO `um_item_types` (`typeid`, `title`, `uid`, `extensions`, `thumbnail`, `interface`, `system`) VALUES
	('1', 'Video', '1000', 'avi,mkv,mp4,wmv,m2ts,mpg', 'ffmpeg', 'video_vlc', '0'),
	('2', 'Audio', '1200', 'mp3,wma,wav', 'audio', 'video_vlc', '0'),
	('3', 'YouTube', '1300', 'yt', 'youtube', 'youtube', '0'),
	('4', 'Web Link', '1400', null, '', 'browser', '0'),
	('5', 'Virtual Folder', '100', null, '', null, '1'),
	('6', 'Image', '1500', 'png,jpg,jpeg,gif,bmp', 'image', 'images', '0')
;
INSERT IGNORE INTO `um_tags` (`tagid`, `title`) VALUES
	('1', 'Unsorted'),
	('2', 'Action'),
	('3', 'Adventure'),
	('4', 'Comedy'),
	('5', 'Crime & Gangs'),
	('6', 'Romance'),
	('7', 'War'),
	('8', 'Horror'),
	('9', 'Musicals'),
	('10', 'Western'),
	('11', 'Technology'),
	('12', 'Epic'),
	('13', 'African'),
	('14', 'Blues'),
	('15', 'Caribbean'),
	('16', 'Classical'),
	('17', 'Folk'),
	('18', 'Electronic'),
	('19', 'Jazz'),
	('20', 'R & B'),
	('21', 'Reggae'),
	('22', 'Pop'),
	('23', 'Rock')
;