-- Create base groups
INSERT INTO `bsa_user_groups` (title, access_login, access_changeaccount, access_media_create, access_media_edit, access_media_delete, access_media_publish, access_admin)
VALUES
	('Users', 1, 1, 1, 0, 0, 0, 0),
	('Moderators', 1, 1, 1, 0, 1, 1, 0),
	('Administrators', 1, 1, 1, 1, 1, 1, 1)
;