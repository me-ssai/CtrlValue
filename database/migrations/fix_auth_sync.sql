-- ============================================================================
-- Supabase Migration Fix: Sync auth.users with public.users
-- ============================================================================
-- Run this script in your Supabase SQL Editor.
-- 
-- 1. Creates a trigger to auto-create public.users when people sign up
-- 2. Backfills any existing auth.users into public.users
-- 3. Updates existing public.user foreign key records if they were recreated 
--    with a new auth.uid() after migration.
-- ============================================================================

-- ────────────────────────────────────────────────────────────────────────────
-- STEP 1: Create the Trigger to Auto-Sync New Signups
-- ────────────────────────────────────────────────────────────────────────────

-- Drop the trigger and function if they already exist to be safe
DROP TRIGGER IF EXISTS on_auth_user_created ON auth.users;
DROP FUNCTION IF EXISTS public.handle_new_user();

-- Create the handler function
CREATE OR REPLACE FUNCTION public.handle_new_user()
RETURNS trigger AS $$
BEGIN
  -- Insert into public.users. If there's an existing email, do nothing.
  INSERT INTO public.users (
    "Id", 
    "Email", 
    "FirstName", 
    "LastName", 
    "TenantId", 
    "IsEmailConfirmed", 
    "PasswordHash", 
    "Role", 
    "IsDeleted", 
    "UpdatedAt", 
    "CreatedAt"
  )
  VALUES (
    NEW.id,
    NEW.email,
    COALESCE(NEW.raw_user_meta_data->>'firstName', 'User'),
    COALESCE(NEW.raw_user_meta_data->>'lastName', ''),
    'default',
    true, -- Mark true since they signed up (or use NEW.email_confirmed_at IS NOT NULL)
    '',   -- Empty password hash because Supabase Auth handles passwords
    'User',
    false,
    NOW(),
    NOW()
  )
  ON CONFLICT ("Email") DO UPDATE
  SET 
    -- If they already exist by email, map them to the new ID if needed
    -- (Though normally this ON CONFLICT is just an extra safety measure)
    "UpdatedAt" = NOW();
    
  RETURN NEW;
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

-- Create the trigger
CREATE TRIGGER on_auth_user_created
  AFTER INSERT ON auth.users
  FOR EACH ROW EXECUTE PROCEDURE public.handle_new_user();

-- ────────────────────────────────────────────────────────────────────────────
-- STEP 2: Backfill Missing Users
-- ────────────────────────────────────────────────────────────────────────────
-- For users who already signed up during the migration gap, let's insert them:

INSERT INTO public.users (
    "Id", 
    "Email", 
    "FirstName", 
    "LastName", 
    "TenantId", 
    "IsEmailConfirmed", 
    "PasswordHash", 
    "Role", 
    "IsDeleted", 
    "UpdatedAt", 
    "CreatedAt"
)
SELECT
    au.id,
    au.email,
    COALESCE(au.raw_user_meta_data->>'firstName', 'User'),
    COALESCE(au.raw_user_meta_data->>'lastName', ''),
    'default',
    true,
    '',
    'User',
    false,
    NOW(),
    NOW()
FROM auth.users au
LEFT JOIN public.users pu ON au.id = pu."Id"
WHERE pu."Id" IS NULL
ON CONFLICT ("Email") DO NOTHING;

-- ────────────────────────────────────────────────────────────────────────────
-- STEP 3: Recover Legacy Foreign Keys
-- ────────────────────────────────────────────────────────────────────────────
-- If you had pre-migration users in `public.users` matching by *email* but with 
-- a DIFFERENT UUID than their new `auth.uid()`, you can link their old workspaces.
-- 
-- The following SQL updates any old `entity_users` pointing to the OLD UUID
-- and maps them to the NEW auth.users UUID based on matching emails.

DO $$
DECLARE
    r RECORD;
BEGIN
    FOR r IN 
        SELECT pu_old."Id" as old_id, au.id as new_id, au.email
        FROM auth.users au
        JOIN public.users pu_old ON au.email = pu_old."Email" AND au.id != pu_old."Id"
    LOOP
        -- Update foreign keys manually
        UPDATE public.entity_users SET "UserId" = r.new_id WHERE "UserId" = r.old_id;
        
        -- After successfully transferring FKs, you might clean up the old user record
        -- DELETE FROM public.users WHERE "Id" = r.old_id;
    END LOOP;
END;
$$;
