CREATE OR REPLACE FUNCTION create_user(name text) RETURNS bigint
    LANGUAGE plpgsql
    SET search_path = ${schema}
    AS $$
DECLARE
  new_id bigint;
BEGIN
  new_id := nextval('user_id_seq');
  INSERT INTO users (id, name) VALUES (new_id, name);
  RETURN new_id;
END;
$$;