-- indexes for RBAC

CREATE UNIQUE INDEX UX_resources_icode ON resources(icode);
CREATE UNIQUE INDEX UX_permissions_icode ON permissions(icode);
CREATE UNIQUE INDEX UX_roles_iname ON roles(iname);
