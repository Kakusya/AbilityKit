# Prepared geometry installation primitive

Integration c61cce1f5 plus WIP; internal application-owned APIs only. Coordinator added CookingPreparedGeometry.cs and unified spatial reads in CookingSpatialInteraction, CookingPortions, CookingExtendedCheckpointValidation and three RecipeLoop operation checks. Original fixture configuration stays immutable; effective installed geometry is a separate owned field. No request accepts arbitrary geometry. The Level owner must build a trusted projection and validate front configuration before calling this internal installer.

Preflight requires complete player identities/valid mutually separated poses with unchanged movement watermarks, unique anchor identities, every live spatial item and processing station reference, and all configured supply anchors. Installation stages frozen geometry, new poses and process dictionaries before assignments; active kitchen worker claims are released with progress preserved. It does not mutate item identities, contents, stock or allocator.

Actual domain filter CookingPreparedGeometryTests passed 2/2, zero skips: moving an existing material source changes real Pickup reach while preserving the item; removing a live material anchor rejects with unchanged checkpoint and geometry. Evidence local/Logs/cooking-execution/cooking-prepared-geometry.* copied by the window owner. No full composite gate has yet validated this additional WIP.

Not complete S08: host preparing publication, trusted equipment/station capability mapping, derived front routes, installed-layout/geometry recovery, preparation clock/service offset and successful next-Level permission handling remain to be implemented. Recipe checkpoint does not yet serialize the effective geometry; the internal primitive is not exposed as a complete public installation flow.

Independent static review identified an empty configured appliance anchor gap. Coordinator tightened preflight to require all configured appliance anchors and reject station anchors unknown to the fixture, and added a third actual rejection control. Its combined gate is pending. The earlier 2/2 result predates this third control.
