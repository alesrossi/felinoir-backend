--
-- PostgreSQL database dump
--

\restrict felinoir

-- Dumped from database version 16.14
-- Dumped by pg_dump version 16.14

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Name: pg_trgm; Type: EXTENSION; Schema: -; Owner: -
--

CREATE EXTENSION IF NOT EXISTS pg_trgm WITH SCHEMA public;


--
-- Name: delete_expired_lobbies(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.delete_expired_lobbies() RETURNS integer
    LANGUAGE sql
    AS $$
    WITH deleted AS (
      DELETE FROM lobbies
       WHERE starts_at < NOW() - INTERVAL '48 hours'
      RETURNING 1
    )
    SELECT count(*)::int FROM deleted;
  $$;


--
-- Name: lobby_bump_version_on_note_change(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.lobby_bump_version_on_note_change() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
  BEGIN
    NEW.version := OLD.version + 1;
    RETURN NEW;
  END $$;


--
-- Name: lobby_bump_version_on_participant_change(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.lobby_bump_version_on_participant_change() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
  BEGIN
    UPDATE lobbies
       SET version = version + 1
     WHERE id = COALESCE(NEW.lobby_id, OLD.lobby_id);
    RETURN NULL;
  END $$;


SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: ai_match_verdicts; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.ai_match_verdicts (
    scraped_title text NOT NULL,
    scraped_original_title text DEFAULT ''::text NOT NULL,
    verdict text NOT NULL,
    tmdb_id integer,
    confidence text NOT NULL,
    checked_tmdb_id integer,
    rejected_tmdb_ids integer[] DEFAULT '{}'::integer[] NOT NULL,
    reason text,
    model text,
    verified_at text NOT NULL,
    CONSTRAINT ai_match_verdicts_confidence_check CHECK ((confidence = ANY (ARRAY['high'::text, 'low'::text]))),
    CONSTRAINT ai_match_verdicts_verdict_check CHECK ((verdict = ANY (ARRAY['match'::text, 'not_a_film'::text, 'unknown'::text])))
);


--
-- Name: cinemas; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.cinemas (
    id text NOT NULL,
    name text NOT NULL,
    chain text,
    address text,
    city text NOT NULL,
    country text DEFAULT 'IT'::text NOT NULL,
    website text NOT NULL,
    schedule_url text NOT NULL,
    created_at text DEFAULT to_char((now() AT TIME ZONE 'UTC'::text), 'YYYY-MM-DD"T"HH24:MI:SS"Z"'::text) NOT NULL,
    updated_at text DEFAULT to_char((now() AT TIME ZONE 'UTC'::text), 'YYYY-MM-DD"T"HH24:MI:SS"Z"'::text) NOT NULL
);


--
-- Name: felix_cache; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.felix_cache (
    id integer DEFAULT 1 NOT NULL,
    cache_name text NOT NULL,
    expires_at text NOT NULL,
    created_at text DEFAULT to_char((now() AT TIME ZONE 'UTC'::text), 'YYYY-MM-DD"T"HH24:MI:SS"Z"'::text) NOT NULL
);


--
-- Name: lobbies; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.lobbies (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    version bigint DEFAULT 1 NOT NULL,
    screening_id integer,
    movie_id integer,
    movie_slug text,
    movie_title text NOT NULL,
    movie_title_en text,
    poster_url text,
    cinema_id text NOT NULL,
    cinema_name text NOT NULL,
    starts_at timestamp with time zone NOT NULL,
    is_ov boolean DEFAULT false NOT NULL,
    is_3d boolean DEFAULT false NOT NULL,
    booking_url text,
    note text DEFAULT ''::text NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT lobbies_note_length CHECK ((char_length(note) <= 500))
);


--
-- Name: lobby_participants; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.lobby_participants (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    lobby_id uuid NOT NULL,
    name text NOT NULL,
    device_hash bytea,
    joined_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT lobby_participants_name_length CHECK (((char_length(name) >= 1) AND (char_length(name) <= 40)))
);


--
-- Name: movies; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.movies (
    id integer NOT NULL,
    title text NOT NULL,
    original_title text,
    director text,
    duration_minutes integer,
    genres text,
    synopsis text,
    poster_url text,
    trailer_url text,
    external_id text,
    rating text,
    created_at text DEFAULT to_char((now() AT TIME ZONE 'UTC'::text), 'YYYY-MM-DD"T"HH24:MI:SS"Z"'::text) NOT NULL,
    updated_at text DEFAULT to_char((now() AT TIME ZONE 'UTC'::text), 'YYYY-MM-DD"T"HH24:MI:SS"Z"'::text) NOT NULL,
    tmdb_id integer,
    imdb_id text,
    tmdb_rating text,
    tmdb_votes text,
    content_rating text,
    writer text,
    actors text,
    released text,
    tmdb_country text,
    tmdb_enriched_at text,
    tmdb_poster_url text,
    tmdb_title text,
    tmdb_plot text,
    tmdb_language text,
    tmdb_genre text,
    tmdb_title_en text,
    tmdb_plot_en text,
    tmdb_genre_en text,
    tmdb_original_title text,
    tmdb_popularity text,
    tmdb_status text,
    tmdb_tagline text,
    tmdb_budget bigint,
    tmdb_revenue bigint,
    tmdb_homepage text,
    tmdb_trailer_url_it text,
    tmdb_trailer_url_en text,
    tmdb_collection text,
    tmdb_production_companies text,
    tmdb_production_countries text,
    tmdb_spoken_languages text,
    tmdb_backdrops text,
    tmdb_videos text,
    tmdb_keywords text,
    tmdb_recommendations text,
    tmdb_watch_providers text,
    rt_rating text,
    metacritic_score text,
    slug text,
    canonical_title text,
    tmdb_match_attempts integer DEFAULT 0 NOT NULL,
    tmdb_last_attempt_at text,
    has_matches text DEFAULT 'FailedMatch'::text NOT NULL,
    tmdb_credits text,
    imdb_rating text,
    imdb_votes text,
    omdb_enriched_at text,
    awards text,
    CONSTRAINT movies_has_matches_check CHECK ((has_matches = ANY (ARRAY['FailedMatch'::text, 'NoMatch'::text, 'FoundMatch'::text])))
);


--
-- Name: movies_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.movies_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: movies_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.movies_id_seq OWNED BY public.movies.id;


--
-- Name: screenings; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.screenings (
    id integer NOT NULL,
    movie_id integer NOT NULL,
    cinema_id text NOT NULL,
    datetime text NOT NULL,
    hall text,
    is_3d boolean DEFAULT false NOT NULL,
    is_ov boolean DEFAULT false NOT NULL,
    subtitle_language text,
    booking_url text,
    meta text,
    created_at text DEFAULT to_char((now() AT TIME ZONE 'UTC'::text), 'YYYY-MM-DD"T"HH24:MI:SS"Z"'::text) NOT NULL,
    updated_at text DEFAULT to_char((now() AT TIME ZONE 'UTC'::text), 'YYYY-MM-DD"T"HH24:MI:SS"Z"'::text) NOT NULL
);


--
-- Name: screenings_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.screenings_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: screenings_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.screenings_id_seq OWNED BY public.screenings.id;


--
-- Name: tmdb_lookup_cache; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.tmdb_lookup_cache (
    scraped_title text NOT NULL,
    scraped_original_title text DEFAULT ''::text NOT NULL,
    tmdb_id integer NOT NULL,
    looked_up_at text DEFAULT to_char((now() AT TIME ZONE 'UTC'::text), 'YYYY-MM-DD"T"HH24:MI:SS"Z"'::text) NOT NULL,
    scraped_poster_url text
);


--
-- Name: movies id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.movies ALTER COLUMN id SET DEFAULT nextval('public.movies_id_seq'::regclass);


--
-- Name: screenings id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.screenings ALTER COLUMN id SET DEFAULT nextval('public.screenings_id_seq'::regclass);


--
-- Name: cinemas cinemas_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.cinemas
    ADD CONSTRAINT cinemas_pkey PRIMARY KEY (id);


--
-- Name: felix_cache felix_cache_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.felix_cache
    ADD CONSTRAINT felix_cache_pkey PRIMARY KEY (id);


--
-- Name: lobbies lobbies_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.lobbies
    ADD CONSTRAINT lobbies_pkey PRIMARY KEY (id);


--
-- Name: lobby_participants lobby_participants_one_per_device; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.lobby_participants
    ADD CONSTRAINT lobby_participants_one_per_device UNIQUE (lobby_id, device_hash);


--
-- Name: lobby_participants lobby_participants_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.lobby_participants
    ADD CONSTRAINT lobby_participants_pkey PRIMARY KEY (id);


--
-- Name: movies movies_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.movies
    ADD CONSTRAINT movies_pkey PRIMARY KEY (id);


--
-- Name: screenings screenings_cinema_id_movie_id_datetime_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.screenings
    ADD CONSTRAINT screenings_cinema_id_movie_id_datetime_key UNIQUE (cinema_id, movie_id, datetime);


--
-- Name: screenings screenings_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.screenings
    ADD CONSTRAINT screenings_pkey PRIMARY KEY (id);


--
-- Name: idx_ai_match_verdicts_key; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX idx_ai_match_verdicts_key ON public.ai_match_verdicts USING btree (scraped_title, scraped_original_title);


--
-- Name: idx_cinemas_city; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_cinemas_city ON public.cinemas USING btree (city);


--
-- Name: idx_movies_canonical_trgm; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_movies_canonical_trgm ON public.movies USING gin (canonical_title public.gin_trgm_ops);


--
-- Name: idx_movies_external_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_movies_external_id ON public.movies USING btree (external_id);


--
-- Name: idx_movies_imdb_id; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX idx_movies_imdb_id ON public.movies USING btree (imdb_id) WHERE (imdb_id IS NOT NULL);


--
-- Name: idx_movies_slug; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX idx_movies_slug ON public.movies USING btree (slug) WHERE (slug IS NOT NULL);


--
-- Name: idx_movies_title; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_movies_title ON public.movies USING btree (title);


--
-- Name: idx_movies_tmdb_id; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX idx_movies_tmdb_id ON public.movies USING btree (tmdb_id) WHERE (tmdb_id IS NOT NULL);


--
-- Name: idx_movies_unique; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX idx_movies_unique ON public.movies USING btree (title, COALESCE(original_title, ''::text));


--
-- Name: idx_screenings_cinema; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_screenings_cinema ON public.screenings USING btree (cinema_id);


--
-- Name: idx_screenings_datetime; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_screenings_datetime ON public.screenings USING btree (datetime);


--
-- Name: idx_screenings_movie; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_screenings_movie ON public.screenings USING btree (movie_id);


--
-- Name: idx_tmdb_lookup_cache_key; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX idx_tmdb_lookup_cache_key ON public.tmdb_lookup_cache USING btree (scraped_title, scraped_original_title);


--
-- Name: lobbies_starts_at_idx; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX lobbies_starts_at_idx ON public.lobbies USING btree (starts_at);


--
-- Name: lobby_participants_name_uniq; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX lobby_participants_name_uniq ON public.lobby_participants USING btree (lobby_id, lower(name));


--
-- Name: lobbies lobbies_bump_version_on_note; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER lobbies_bump_version_on_note BEFORE UPDATE OF note ON public.lobbies FOR EACH ROW WHEN ((old.note IS DISTINCT FROM new.note)) EXECUTE FUNCTION public.lobby_bump_version_on_note_change();


--
-- Name: lobby_participants lobby_participants_bump_version; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER lobby_participants_bump_version AFTER INSERT OR DELETE OR UPDATE ON public.lobby_participants FOR EACH ROW EXECUTE FUNCTION public.lobby_bump_version_on_participant_change();


--
-- Name: lobby_participants lobby_participants_lobby_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.lobby_participants
    ADD CONSTRAINT lobby_participants_lobby_id_fkey FOREIGN KEY (lobby_id) REFERENCES public.lobbies(id) ON DELETE CASCADE;


--
-- Name: screenings screenings_cinema_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.screenings
    ADD CONSTRAINT screenings_cinema_id_fkey FOREIGN KEY (cinema_id) REFERENCES public.cinemas(id) ON DELETE CASCADE;


--
-- Name: screenings screenings_movie_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.screenings
    ADD CONSTRAINT screenings_movie_id_fkey FOREIGN KEY (movie_id) REFERENCES public.movies(id) ON DELETE CASCADE;


--
-- PostgreSQL database dump complete
--

\unrestrict felinoir

