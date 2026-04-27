CREATE OR REPLACE FUNCTION rename_user(user_id bigint, new_name text) RETURNS integer
    LANGUAGE plpgsql
    SET search_path = ${schema}
    AS $$
DECLARE
  affected_rows integer;
BEGIN
  UPDATE users SET name = new_name WHERE id = user_id;
  GET DIAGNOSTICS affected_rows = ROW_COUNT;
  RETURN affected_rows;
END;
$$;