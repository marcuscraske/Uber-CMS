SET FOREIGN_KEY_CHECKS=0;

-- Drop tables
DROP TABLE IF EXISTS `bsa_user_log`;
DROP TABLE IF EXISTS `bsa_users`;
DROP TABLE IF EXISTS `bsa_user_groups`;
DROP TABLE IF EXISTS `bsa_activations`;
DROP TABLE IF EXISTS `bsa_recovery_email`;
DROP TABLE IF EXISTS `bsa_recovery_sqa_attempts`;
DROP TABLE IF EXISTS `bsa_user_bans`;
DROP TABLE IF EXISTS `bsa_failed_logins`;

-- Drop procedures
DROP PROCEDURE IF EXISTS `bsa_register`;
DROP PROCEDURE IF EXISTS `bsa_change_email`;

SET FOREIGN_KEY_CHECKS=1;