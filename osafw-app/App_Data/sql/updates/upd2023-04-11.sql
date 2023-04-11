ALTER TABLE users ADD   
  ui_theme                 TINYINT NOT NULL DEFAULT 0, -- 0--default theme
  ui_mode                  TINYINT NOT NULL DEFAULT 0; -- 0--auto, 10-light, 20-dark
