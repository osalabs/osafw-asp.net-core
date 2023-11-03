-- events only accessible for Site Admin
UPDATE lookup_manager_tables SET access_level=100 where tname='events';
