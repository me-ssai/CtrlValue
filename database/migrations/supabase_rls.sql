-- ============================================================================
-- Supabase Row-Level Security (RLS) for Project Z
-- ============================================================================
-- Prerequisites:
--   • users."Id" must match auth.uid() from Supabase Auth
--   • EntityRole enum: OWNER=0, VIEWER=1, EDITOR=2 (stored as integer)
--   • All tables use soft-delete ("IsDeleted" boolean)
-- ============================================================================

-- ────────────────────────────────────────────────────────────────────────────
-- 1) HELPER FUNCTIONS (SECURITY DEFINER)
-- ────────────────────────────────────────────────────────────────────────────

CREATE OR REPLACE FUNCTION public.get_user_id() RETURNS uuid
LANGUAGE sql STABLE SECURITY DEFINER AS $$
  SELECT auth.uid();
$$;
REVOKE EXECUTE ON FUNCTION public.get_user_id() FROM anon, authenticated;

CREATE OR REPLACE FUNCTION public.get_user_tenant() RETURNS text
LANGUAGE sql STABLE SECURITY DEFINER AS $$
  SELECT u."TenantId" FROM public."users" u WHERE u."Id" = auth.uid();
$$;
REVOKE EXECUTE ON FUNCTION public.get_user_tenant() FROM anon, authenticated;

CREATE OR REPLACE FUNCTION public.is_entity_member(e uuid) RETURNS boolean
LANGUAGE sql STABLE SECURITY DEFINER AS $$
  SELECT EXISTS (
    SELECT 1 FROM public."entity_users" eu
    WHERE eu."EntityId" = e
      AND eu."UserId" = auth.uid()
      AND eu."IsDeleted" = false
  );
$$;
REVOKE EXECUTE ON FUNCTION public.is_entity_member(uuid) FROM anon, authenticated;

CREATE OR REPLACE FUNCTION public.is_entity_admin(e uuid) RETURNS boolean
LANGUAGE sql STABLE SECURITY DEFINER AS $$
  SELECT EXISTS (
    SELECT 1 FROM public."entity_users" eu
    WHERE eu."EntityId" = e
      AND eu."UserId" = auth.uid()
      AND eu."IsDeleted" = false
      AND eu."Role" = 0  -- OWNER
  );
$$;
REVOKE EXECUTE ON FUNCTION public.is_entity_admin(uuid) FROM anon, authenticated;

CREATE OR REPLACE FUNCTION public.is_entity_editor_or_above(e uuid) RETURNS boolean
LANGUAGE sql STABLE SECURITY DEFINER AS $$
  SELECT EXISTS (
    SELECT 1 FROM public."entity_users" eu
    WHERE eu."EntityId" = e
      AND eu."UserId" = auth.uid()
      AND eu."IsDeleted" = false
      AND eu."Role" IN (0, 2)  -- OWNER or EDITOR
  );
$$;
REVOKE EXECUTE ON FUNCTION public.is_entity_editor_or_above(uuid) FROM anon, authenticated;


-- ────────────────────────────────────────────────────────────────────────────
-- 2) ENABLE RLS
-- ────────────────────────────────────────────────────────────────────────────

ALTER TABLE public."users"                               ENABLE ROW LEVEL SECURITY;
ALTER TABLE public."entity"                              ENABLE ROW LEVEL SECURITY;
ALTER TABLE public."entity_users"                        ENABLE ROW LEVEL SECURITY;
ALTER TABLE public."account"                             ENABLE ROW LEVEL SECURITY;
ALTER TABLE public."txn"                                 ENABLE ROW LEVEL SECURITY;
ALTER TABLE public."position"                            ENABLE ROW LEVEL SECURITY;
ALTER TABLE public."category"                            ENABLE ROW LEVEL SECURITY;
ALTER TABLE public."valuation"                           ENABLE ROW LEVEL SECURITY;
ALTER TABLE public."depreciation_schedule"               ENABLE ROW LEVEL SECURITY;
ALTER TABLE public."budget"                              ENABLE ROW LEVEL SECURITY;
ALTER TABLE public."imported_transactions_files"         ENABLE ROW LEVEL SECURITY;
ALTER TABLE public."imported_transactions_files_staging" ENABLE ROW LEVEL SECURITY;
ALTER TABLE public."instrument"                          ENABLE ROW LEVEL SECURITY;
ALTER TABLE public."price_history"                       ENABLE ROW LEVEL SECURITY;


-- ────────────────────────────────────────────────────────────────────────────
-- 3) POLICIES
-- ────────────────────────────────────────────────────────────────────────────

-- ── USERS ──
CREATE POLICY "users_select_own" ON public."users"
  FOR SELECT TO authenticated
  USING ("Id" = (SELECT public.get_user_id()));

CREATE POLICY "users_update_own" ON public."users"
  FOR UPDATE TO authenticated
  USING ("Id" = (SELECT public.get_user_id()))
  WITH CHECK ("Id" = (SELECT public.get_user_id()));

CREATE POLICY "users_insert_self" ON public."users"
  FOR INSERT TO authenticated
  WITH CHECK ("Id" = (SELECT public.get_user_id()));

-- ── ENTITY ──
CREATE POLICY "entity_select_member" ON public."entity"
  FOR SELECT TO authenticated
  USING ("IsDeleted" = false AND public.is_entity_member("Id"));

CREATE POLICY "entity_insert" ON public."entity"
  FOR INSERT TO authenticated
  WITH CHECK ("TenantId" = (SELECT public.get_user_tenant()));

CREATE POLICY "entity_update_admin" ON public."entity"
  FOR UPDATE TO authenticated
  USING (public.is_entity_admin("Id"))
  WITH CHECK (public.is_entity_admin("Id"));

CREATE POLICY "entity_delete_admin" ON public."entity"
  FOR DELETE TO authenticated
  USING (public.is_entity_admin("Id"));

-- ── ENTITY_USERS ──
CREATE POLICY "entity_users_select" ON public."entity_users"
  FOR SELECT TO authenticated
  USING ("IsDeleted" = false AND (
    "UserId" = (SELECT public.get_user_id())
    OR public.is_entity_member("EntityId")
  ));

CREATE POLICY "entity_users_insert_admin" ON public."entity_users"
  FOR INSERT TO authenticated
  WITH CHECK (public.is_entity_admin("EntityId"));

CREATE POLICY "entity_users_update_admin" ON public."entity_users"
  FOR UPDATE TO authenticated
  USING (public.is_entity_admin("EntityId"))
  WITH CHECK (public.is_entity_admin("EntityId"));

CREATE POLICY "entity_users_delete_self_or_admin" ON public."entity_users"
  FOR DELETE TO authenticated
  USING ("UserId" = (SELECT public.get_user_id()) OR public.is_entity_admin("EntityId"));

-- ── ACCOUNT ──
CREATE POLICY "account_select_member" ON public."account"
  FOR SELECT TO authenticated
  USING ("IsDeleted" = false AND public.is_entity_member("EntityId"));

CREATE POLICY "account_insert_editor" ON public."account"
  FOR INSERT TO authenticated
  WITH CHECK (public.is_entity_editor_or_above("EntityId")
    AND "TenantId" = (SELECT public.get_user_tenant()));

CREATE POLICY "account_update_editor" ON public."account"
  FOR UPDATE TO authenticated
  USING (public.is_entity_editor_or_above("EntityId"))
  WITH CHECK (public.is_entity_editor_or_above("EntityId"));

CREATE POLICY "account_delete_admin" ON public."account"
  FOR DELETE TO authenticated
  USING (public.is_entity_admin("EntityId"));

-- ── TXN ──
CREATE POLICY "txn_select_member" ON public."txn"
  FOR SELECT TO authenticated
  USING ("IsDeleted" = false AND public.is_entity_member("EntityId"));

CREATE POLICY "txn_insert_editor" ON public."txn"
  FOR INSERT TO authenticated
  WITH CHECK (public.is_entity_editor_or_above("EntityId")
    AND "TenantId" = (SELECT public.get_user_tenant()));

CREATE POLICY "txn_update_editor" ON public."txn"
  FOR UPDATE TO authenticated
  USING (public.is_entity_editor_or_above("EntityId"))
  WITH CHECK (public.is_entity_editor_or_above("EntityId"));

CREATE POLICY "txn_delete_admin" ON public."txn"
  FOR DELETE TO authenticated
  USING (public.is_entity_admin("EntityId"));

-- ── POSITION (via AccountId → account) ──
CREATE POLICY "position_select_member" ON public."position"
  FOR SELECT TO authenticated
  USING ("IsDeleted" = false AND EXISTS (
    SELECT 1 FROM public."account" a
    WHERE a."Id" = "AccountId" AND a."IsDeleted" = false
      AND public.is_entity_member(a."EntityId")
  ));

CREATE POLICY "position_insert_editor" ON public."position"
  FOR INSERT TO authenticated
  WITH CHECK (EXISTS (
    SELECT 1 FROM public."account" a
    WHERE a."Id" = "AccountId" AND a."IsDeleted" = false
      AND public.is_entity_editor_or_above(a."EntityId")
  ) AND "TenantId" = (SELECT public.get_user_tenant()));

CREATE POLICY "position_update_editor" ON public."position"
  FOR UPDATE TO authenticated
  USING (EXISTS (
    SELECT 1 FROM public."account" a
    WHERE a."Id" = "AccountId" AND public.is_entity_editor_or_above(a."EntityId")
  ))
  WITH CHECK (EXISTS (
    SELECT 1 FROM public."account" a
    WHERE a."Id" = "AccountId" AND public.is_entity_editor_or_above(a."EntityId")
  ));

CREATE POLICY "position_delete_admin" ON public."position"
  FOR DELETE TO authenticated
  USING (EXISTS (
    SELECT 1 FROM public."account" a
    WHERE a."Id" = "AccountId" AND public.is_entity_admin(a."EntityId")
  ));

-- ── CATEGORY ──
CREATE POLICY "category_select_member" ON public."category"
  FOR SELECT TO authenticated
  USING ("IsDeleted" = false AND public.is_entity_member("EntityId"));

CREATE POLICY "category_insert_editor" ON public."category"
  FOR INSERT TO authenticated
  WITH CHECK (public.is_entity_editor_or_above("EntityId")
    AND "TenantId" = (SELECT public.get_user_tenant()));

CREATE POLICY "category_update_editor" ON public."category"
  FOR UPDATE TO authenticated
  USING (public.is_entity_editor_or_above("EntityId"))
  WITH CHECK (public.is_entity_editor_or_above("EntityId"));

CREATE POLICY "category_delete_admin" ON public."category"
  FOR DELETE TO authenticated
  USING (public.is_entity_admin("EntityId"));

-- ── VALUATION (via AccountId → account) ──
CREATE POLICY "valuation_select_member" ON public."valuation"
  FOR SELECT TO authenticated
  USING ("IsDeleted" = false AND EXISTS (
    SELECT 1 FROM public."account" a
    WHERE a."Id" = "AccountId" AND a."IsDeleted" = false
      AND public.is_entity_member(a."EntityId")
  ));

CREATE POLICY "valuation_insert_editor" ON public."valuation"
  FOR INSERT TO authenticated
  WITH CHECK (EXISTS (
    SELECT 1 FROM public."account" a
    WHERE a."Id" = "AccountId" AND a."IsDeleted" = false
      AND public.is_entity_editor_or_above(a."EntityId")
  ) AND "TenantId" = (SELECT public.get_user_tenant()));

CREATE POLICY "valuation_update_editor" ON public."valuation"
  FOR UPDATE TO authenticated
  USING (EXISTS (
    SELECT 1 FROM public."account" a
    WHERE a."Id" = "AccountId" AND public.is_entity_editor_or_above(a."EntityId")
  ))
  WITH CHECK (EXISTS (
    SELECT 1 FROM public."account" a
    WHERE a."Id" = "AccountId" AND public.is_entity_editor_or_above(a."EntityId")
  ));

CREATE POLICY "valuation_delete_admin" ON public."valuation"
  FOR DELETE TO authenticated
  USING (EXISTS (
    SELECT 1 FROM public."account" a
    WHERE a."Id" = "AccountId" AND public.is_entity_admin(a."EntityId")
  ));

-- ── DEPRECIATION_SCHEDULE (via AccountId → account) ──
CREATE POLICY "depreciation_select_member" ON public."depreciation_schedule"
  FOR SELECT TO authenticated
  USING ("IsDeleted" = false AND EXISTS (
    SELECT 1 FROM public."account" a
    WHERE a."Id" = "AccountId" AND a."IsDeleted" = false
      AND public.is_entity_member(a."EntityId")
  ));

CREATE POLICY "depreciation_insert_editor" ON public."depreciation_schedule"
  FOR INSERT TO authenticated
  WITH CHECK (EXISTS (
    SELECT 1 FROM public."account" a
    WHERE a."Id" = "AccountId" AND a."IsDeleted" = false
      AND public.is_entity_editor_or_above(a."EntityId")
  ) AND "TenantId" = (SELECT public.get_user_tenant()));

CREATE POLICY "depreciation_update_editor" ON public."depreciation_schedule"
  FOR UPDATE TO authenticated
  USING (EXISTS (
    SELECT 1 FROM public."account" a
    WHERE a."Id" = "AccountId" AND public.is_entity_editor_or_above(a."EntityId")
  ))
  WITH CHECK (EXISTS (
    SELECT 1 FROM public."account" a
    WHERE a."Id" = "AccountId" AND public.is_entity_editor_or_above(a."EntityId")
  ));

CREATE POLICY "depreciation_delete_admin" ON public."depreciation_schedule"
  FOR DELETE TO authenticated
  USING (EXISTS (
    SELECT 1 FROM public."account" a
    WHERE a."Id" = "AccountId" AND public.is_entity_admin(a."EntityId")
  ));

-- ── BUDGET ──
CREATE POLICY "budget_select_member" ON public."budget"
  FOR SELECT TO authenticated
  USING ("IsDeleted" = false AND public.is_entity_member("EntityId"));

CREATE POLICY "budget_insert_editor" ON public."budget"
  FOR INSERT TO authenticated
  WITH CHECK (public.is_entity_editor_or_above("EntityId")
    AND "TenantId" = (SELECT public.get_user_tenant()));

CREATE POLICY "budget_update_editor" ON public."budget"
  FOR UPDATE TO authenticated
  USING (public.is_entity_editor_or_above("EntityId"))
  WITH CHECK (public.is_entity_editor_or_above("EntityId"));

CREATE POLICY "budget_delete_admin" ON public."budget"
  FOR DELETE TO authenticated
  USING (public.is_entity_admin("EntityId"));

-- ── IMPORTED_TRANSACTIONS_FILES ──
CREATE POLICY "imported_files_select_member" ON public."imported_transactions_files"
  FOR SELECT TO authenticated
  USING ("IsDeleted" = false AND public.is_entity_member("EntityId"));

CREATE POLICY "imported_files_insert_editor" ON public."imported_transactions_files"
  FOR INSERT TO authenticated
  WITH CHECK (public.is_entity_editor_or_above("EntityId")
    AND "TenantId" = (SELECT public.get_user_tenant()));

CREATE POLICY "imported_files_update_editor" ON public."imported_transactions_files"
  FOR UPDATE TO authenticated
  USING (public.is_entity_editor_or_above("EntityId"))
  WITH CHECK (public.is_entity_editor_or_above("EntityId"));

CREATE POLICY "imported_files_delete_admin" ON public."imported_transactions_files"
  FOR DELETE TO authenticated
  USING (public.is_entity_admin("EntityId"));

-- ── IMPORTED_TRANSACTIONS_FILES_STAGING ──
CREATE POLICY "imported_staging_select_member" ON public."imported_transactions_files_staging"
  FOR SELECT TO authenticated
  USING ("IsDeleted" = false AND public.is_entity_member("EntityId"));

CREATE POLICY "imported_staging_insert_editor" ON public."imported_transactions_files_staging"
  FOR INSERT TO authenticated
  WITH CHECK (public.is_entity_editor_or_above("EntityId")
    AND "TenantId" = (SELECT public.get_user_tenant()));

CREATE POLICY "imported_staging_update_editor" ON public."imported_transactions_files_staging"
  FOR UPDATE TO authenticated
  USING (public.is_entity_editor_or_above("EntityId"))
  WITH CHECK (public.is_entity_editor_or_above("EntityId"));

CREATE POLICY "imported_staging_delete_admin" ON public."imported_transactions_files_staging"
  FOR DELETE TO authenticated
  USING (public.is_entity_admin("EntityId"));

-- ── INSTRUMENT (tenant-scoped, no EntityId) ──
CREATE POLICY "instrument_select_tenant" ON public."instrument"
  FOR SELECT TO authenticated
  USING ("IsDeleted" = false AND "TenantId" = (SELECT public.get_user_tenant()));

CREATE POLICY "instrument_insert_tenant" ON public."instrument"
  FOR INSERT TO authenticated
  WITH CHECK ("TenantId" = (SELECT public.get_user_tenant()));

CREATE POLICY "instrument_update_tenant" ON public."instrument"
  FOR UPDATE TO authenticated
  USING ("TenantId" = (SELECT public.get_user_tenant()))
  WITH CHECK ("TenantId" = (SELECT public.get_user_tenant()));

CREATE POLICY "instrument_delete_tenant" ON public."instrument"
  FOR DELETE TO authenticated
  USING ("TenantId" = (SELECT public.get_user_tenant()));

-- ── PRICE_HISTORY (tenant-scoped via instrument) ──
CREATE POLICY "price_history_select_tenant" ON public."price_history"
  FOR SELECT TO authenticated
  USING ("IsDeleted" = false AND EXISTS (
    SELECT 1 FROM public."instrument" i
    WHERE i."Id" = "InstrumentId" AND i."IsDeleted" = false
      AND i."TenantId" = (SELECT public.get_user_tenant())
  ));

CREATE POLICY "price_history_insert_tenant" ON public."price_history"
  FOR INSERT TO authenticated
  WITH CHECK (EXISTS (
    SELECT 1 FROM public."instrument" i
    WHERE i."Id" = "InstrumentId"
      AND i."TenantId" = (SELECT public.get_user_tenant())
  ) AND "TenantId" = (SELECT public.get_user_tenant()));

CREATE POLICY "price_history_update_tenant" ON public."price_history"
  FOR UPDATE TO authenticated
  USING (EXISTS (
    SELECT 1 FROM public."instrument" i
    WHERE i."Id" = "InstrumentId"
      AND i."TenantId" = (SELECT public.get_user_tenant())
  ))
  WITH CHECK (EXISTS (
    SELECT 1 FROM public."instrument" i
    WHERE i."Id" = "InstrumentId"
      AND i."TenantId" = (SELECT public.get_user_tenant())
  ));

CREATE POLICY "price_history_delete_tenant" ON public."price_history"
  FOR DELETE TO authenticated
  USING (EXISTS (
    SELECT 1 FROM public."instrument" i
    WHERE i."Id" = "InstrumentId"
      AND i."TenantId" = (SELECT public.get_user_tenant())
  ));
