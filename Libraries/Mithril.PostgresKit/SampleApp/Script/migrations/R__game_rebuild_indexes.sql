CREATE OR REPLACE PROCEDURE rebuild_indexes()
    LANGUAGE plpgsql
    SET search_path = ${schema}
    AS $$
BEGIN
  RAISE NOTICE 'Rebuilding indexes...';
END;
$$;