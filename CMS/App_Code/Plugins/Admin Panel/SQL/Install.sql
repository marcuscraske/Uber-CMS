CREATE TABLE IF NOT EXISTS `admin_panel_pages`
(
	pageid INT PRIMARY KEY AUTO_INCREMENT,
	classpath TEXT,
	method TEXT,
	title TEXT,
	category TEXT,
	menu_icon TEXT
);