CREATE OR REPLACE FUNCTION list_recent_users(limit_count integer) 
RETURNS TABLE(id bigint, name text, created_at timestamp with time zone)
    LANGUAGE sql STABLE
    SET search_path = ${schema}
    AS $$
  SELECT u.id, u.name, u.created_at
  FROM users u
  ORDER BY u.created_at DESC
  LIMIT limit_count;
$$;