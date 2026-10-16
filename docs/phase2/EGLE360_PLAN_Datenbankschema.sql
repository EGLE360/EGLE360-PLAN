-- EGLE360 PLAN – Relationales Datenmodell (Phase 2, Entwurf)
-- Zielsystem: PostgreSQL 17. Primärschlüssel: UUIDv7, von der Anwendung erzeugt.

CREATE SCHEMA IF NOT EXISTS plan;
SET search_path = plan;

-- Organisation, Benutzer, Rechte ------------------------------------------

CREATE TABLE organization (
    id              uuid PRIMARY KEY,
    name            text NOT NULL,
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE app_user (
    id              uuid PRIMARY KEY,
    organization_id uuid NOT NULL REFERENCES organization(id),
    external_subject text NOT NULL UNIQUE,           -- OIDC "sub"
    email           text NOT NULL,
    display_name    text NOT NULL,
    is_active       boolean NOT NULL DEFAULT true,
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE role (
    id              uuid PRIMARY KEY,
    organization_id uuid REFERENCES organization(id), -- NULL = Systemrolle
    name            text NOT NULL,
    permissions     text[] NOT NULL,                  -- z. B. {'markup.write','quantity.approve'}
    UNIQUE (organization_id, name)
);

CREATE TABLE project (
    id              uuid PRIMARY KEY,
    organization_id uuid NOT NULL REFERENCES organization(id),
    code            text NOT NULL,
    name            text NOT NULL,
    country         char(2) NOT NULL CHECK (country IN ('AT','DE')),
    default_units   text NOT NULL DEFAULT 'metric',
    created_at      timestamptz NOT NULL DEFAULT now(),
    archived_at     timestamptz,
    UNIQUE (organization_id, code)
);

CREATE TABLE project_member (
    project_id      uuid NOT NULL REFERENCES project(id) ON DELETE CASCADE,
    user_id         uuid NOT NULL REFERENCES app_user(id),
    role_id         uuid NOT NULL REFERENCES role(id),
    PRIMARY KEY (project_id, user_id)
);

-- Projektstruktur ---------------------------------------------------------

CREATE TABLE building (
    id              uuid PRIMARY KEY,
    project_id      uuid NOT NULL REFERENCES project(id) ON DELETE CASCADE,
    name            text NOT NULL
);

CREATE TABLE storey (
    id              uuid PRIMARY KEY,
    building_id     uuid NOT NULL REFERENCES building(id) ON DELETE CASCADE,
    name            text NOT NULL,                    -- z. B. EG, 1.OG
    elevation_m     numeric(10,3),
    sort_order      int NOT NULL DEFAULT 0
);

CREATE TABLE trade (                                   -- Gewerk
    id              uuid PRIMARY KEY,
    project_id      uuid NOT NULL REFERENCES project(id) ON DELETE CASCADE,
    code            text NOT NULL,
    name            text NOT NULL,
    UNIQUE (project_id, code)
);

-- Dokumente und Planversionen --------------------------------------------

CREATE TABLE stored_file (
    sha256          bytea PRIMARY KEY,                -- inhaltsadressiert, unveränderlich
    size_bytes      bigint NOT NULL,
    media_type      text NOT NULL,
    storage_key     text NOT NULL,
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE document (                                -- logischer Plan / logisches Dokument
    id              uuid PRIMARY KEY,
    project_id      uuid NOT NULL REFERENCES project(id) ON DELETE CASCADE,
    kind            text NOT NULL CHECK (kind IN ('plan','document','model')),
    sheet_number    text,                             -- Plannummer
    title           text NOT NULL,
    trade_id        uuid REFERENCES trade(id),
    building_id     uuid REFERENCES building(id),
    storey_id       uuid REFERENCES storey(id),
    construction_section text,                        -- Bauabschnitt
    author_company  text,                             -- Planverfasser
    properties      jsonb NOT NULL DEFAULT '{}',
    created_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE (project_id, sheet_number)
);

CREATE TABLE document_version (
    id              uuid PRIMARY KEY,
    document_id     uuid NOT NULL REFERENCES document(id) ON DELETE CASCADE,
    revision_index  text NOT NULL,                    -- Planindex
    file_sha256     bytea NOT NULL REFERENCES stored_file(sha256),
    plan_date       date,
    status          text NOT NULL DEFAULT 'draft'
                    CHECK (status IN ('draft','for_review','approved','superseded','archived')),
    approved_by     uuid REFERENCES app_user(id),
    approved_at     timestamptz,
    uploaded_by     uuid NOT NULL REFERENCES app_user(id),
    created_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE (document_id, revision_index)
);

CREATE TABLE page (
    id              uuid PRIMARY KEY,
    document_version_id uuid NOT NULL REFERENCES document_version(id) ON DELETE CASCADE,
    page_index      int NOT NULL,
    page_label      text,
    width_pt        numeric(12,4) NOT NULL,
    height_pt       numeric(12,4) NOT NULL,
    rotation        smallint NOT NULL DEFAULT 0 CHECK (rotation IN (0,90,180,270)),
    UNIQUE (document_version_id, page_index)
);

CREATE TABLE viewport_scale (                          -- Maßstab je Seite oder Ansichtsfenster
    id              uuid PRIMARY KEY,
    page_id         uuid NOT NULL REFERENCES page(id) ON DELETE CASCADE,
    region          jsonb,                            -- NULL = ganze Seite, sonst Polygon in PDF-Koordinaten
    paper_length_pt numeric(18,6) NOT NULL,
    real_length     numeric(18,6) NOT NULL,
    real_unit       text NOT NULL CHECK (real_unit IN ('mm','cm','m')),
    calibrated      boolean NOT NULL DEFAULT false,
    created_by      uuid NOT NULL REFERENCES app_user(id),
    created_at      timestamptz NOT NULL DEFAULT now()
);

-- Markups und Messungen ---------------------------------------------------

CREATE TABLE markup (
    id              uuid PRIMARY KEY,
    page_id         uuid NOT NULL REFERENCES page(id) ON DELETE CASCADE,
    pdf_annot_name  text,                             -- /NM der PDF-Annotation
    markup_type     text NOT NULL,                    -- line, polyline, area, count, cloud, text ...
    geometry        jsonb NOT NULL,                   -- Punkte und Segmente in PDF-Koordinaten
    style           jsonb NOT NULL DEFAULT '{}',
    subject         text,
    label           text,
    comment         text,
    status          text,
    priority        smallint,
    assignee_id     uuid REFERENCES app_user(id),
    layer           text,
    space_id        uuid,                             -- FK unten
    locked          boolean NOT NULL DEFAULT false,
    properties      jsonb NOT NULL DEFAULT '{}',      -- benutzerdefinierte Spalten
    carried_from    uuid REFERENCES markup(id),       -- Herkunft bei Planaustausch
    revision        bigint NOT NULL DEFAULT 1,        -- für Synchronisation
    created_by      uuid NOT NULL REFERENCES app_user(id),
    created_at      timestamptz NOT NULL DEFAULT now(),
    modified_by     uuid NOT NULL REFERENCES app_user(id),
    modified_at     timestamptz NOT NULL DEFAULT now(),
    deleted_at      timestamptz
);
CREATE INDEX markup_page_idx ON markup(page_id) WHERE deleted_at IS NULL;

CREATE TABLE space (                                   -- Bereich / Raum im Plan
    id              uuid PRIMARY KEY,
    page_id         uuid NOT NULL REFERENCES page(id) ON DELETE CASCADE,
    parent_id       uuid REFERENCES space(id),
    name            text NOT NULL,
    geometry        jsonb NOT NULL
);
ALTER TABLE markup ADD CONSTRAINT markup_space_fk FOREIGN KEY (space_id) REFERENCES space(id);

CREATE TABLE measurement (
    markup_id       uuid PRIMARY KEY REFERENCES markup(id) ON DELETE CASCADE,
    scale_id        uuid NOT NULL REFERENCES viewport_scale(id),
    kind            text NOT NULL CHECK (kind IN ('length','polylength','perimeter','area','volume',
                                                  'count','diameter','radius','angle','custom')),
    depth           numeric(18,6),
    slope           numeric(18,6),
    length_value    numeric(18,6),
    area_value      numeric(18,6),
    wall_area_value numeric(18,6),
    volume_value    numeric(18,6),
    count_value     int,
    angle_deg       numeric(10,4),
    engine_version  text NOT NULL                     -- Version des Rechenkerns zur Reproduzierbarkeit
);

CREATE TABLE markup_history (
    id              bigserial PRIMARY KEY,
    markup_id       uuid NOT NULL REFERENCES markup(id) ON DELETE CASCADE,
    revision        bigint NOT NULL,
    snapshot        jsonb NOT NULL,
    changed_by      uuid NOT NULL REFERENCES app_user(id),
    changed_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE (markup_id, revision)
);

-- BIM ------------------------------------------------------------------------

CREATE TABLE bim_model_version (
    id              uuid PRIMARY KEY,
    document_id     uuid NOT NULL REFERENCES document(id) ON DELETE CASCADE,  -- kind = 'model'
    version_label   text NOT NULL,
    file_sha256     bytea NOT NULL REFERENCES stored_file(sha256),
    ifc_schema      text NOT NULL CHECK (ifc_schema IN ('IFC2X3','IFC4','IFC4X3')),
    discipline      text,                              -- Architektur, Tragwerk, HLS, Elektro
    processed_at    timestamptz,
    processing_state text NOT NULL DEFAULT 'pending',
    UNIQUE (document_id, version_label)
);

CREATE TABLE ifc_element (
    id              uuid PRIMARY KEY,
    model_version_id uuid NOT NULL REFERENCES bim_model_version(id) ON DELETE CASCADE,
    global_id       char(22) NOT NULL,
    ifc_class       text NOT NULL,
    name            text,
    type_name       text,
    storey_id       uuid REFERENCES storey(id),
    space_global_id char(22),
    material        text,
    geometry_hash   bytea,                             -- für Modellvergleich
    bbox            numeric[],                         -- minx,miny,minz,maxx,maxy,maxz
    UNIQUE (model_version_id, global_id)
);
CREATE INDEX ifc_element_class_idx ON ifc_element(model_version_id, ifc_class);

CREATE TABLE ifc_property (
    element_id      uuid NOT NULL REFERENCES ifc_element(id) ON DELETE CASCADE,
    pset            text NOT NULL,
    name            text NOT NULL,
    value           jsonb,
    PRIMARY KEY (element_id, pset, name)
);

CREATE TABLE ifc_classification (
    element_id      uuid NOT NULL REFERENCES ifc_element(id) ON DELETE CASCADE,
    system          text NOT NULL,
    code            text NOT NULL,
    PRIMARY KEY (element_id, system, code)
);

CREATE TABLE plan_model_link (                          -- 2D/3D-Verknüpfung
    id              uuid PRIMARY KEY,
    page_id         uuid NOT NULL REFERENCES page(id) ON DELETE CASCADE,
    model_version_id uuid NOT NULL REFERENCES bim_model_version(id) ON DELETE CASCADE,
    storey_id       uuid REFERENCES storey(id),
    transform       jsonb NOT NULL,                     -- PDF-Koordinaten -> Modellkoordinaten
    confirmed_by    uuid REFERENCES app_user(id)
);

CREATE TABLE markup_element_link (
    markup_id       uuid NOT NULL REFERENCES markup(id) ON DELETE CASCADE,
    element_id      uuid NOT NULL REFERENCES ifc_element(id) ON DELETE CASCADE,
    confidence      numeric(4,3),
    confirmed_by    uuid REFERENCES app_user(id),
    PRIMARY KEY (markup_id, element_id)
);

CREATE TABLE clash_test (
    id              uuid PRIMARY KEY,
    project_id      uuid NOT NULL REFERENCES project(id) ON DELETE CASCADE,
    name            text NOT NULL,
    mode            text NOT NULL CHECK (mode IN ('hard','clearance')),
    tolerance_m     numeric(10,4) NOT NULL DEFAULT 0,
    set_a           jsonb NOT NULL,                     -- Auswahlregeln
    set_b           jsonb NOT NULL,
    run_at          timestamptz,
    created_by      uuid NOT NULL REFERENCES app_user(id)
);

CREATE TABLE clash_result (
    id              uuid PRIMARY KEY,
    clash_test_id   uuid NOT NULL REFERENCES clash_test(id) ON DELETE CASCADE,
    element_a_id    uuid NOT NULL REFERENCES ifc_element(id),
    element_b_id    uuid NOT NULL REFERENCES ifc_element(id),
    distance_m      numeric(10,4),
    point           numeric[],
    group_key       text,
    status          text NOT NULL DEFAULT 'new',
    issue_id        uuid
);

CREATE TABLE model_comparison (
    id              uuid PRIMARY KEY,
    base_version_id uuid NOT NULL REFERENCES bim_model_version(id),
    target_version_id uuid NOT NULL REFERENCES bim_model_version(id),
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE model_change (
    comparison_id   uuid NOT NULL REFERENCES model_comparison(id) ON DELETE CASCADE,
    global_id       char(22) NOT NULL,
    change_type     text NOT NULL CHECK (change_type IN ('added','removed','geometry','position','property',
                                                         'material','classification','quantity')),
    details         jsonb,
    PRIMARY KEY (comparison_id, global_id, change_type)
);

-- Mengen, Kosten, AVA ------------------------------------------------------

CREATE TABLE quantity (
    id              uuid PRIMARY KEY,
    project_id      uuid NOT NULL REFERENCES project(id) ON DELETE CASCADE,
    source          text NOT NULL CHECK (source IN ('pdf_measure','ifc_qto','ifc_geometry','manual')),
    markup_id       uuid REFERENCES markup(id),
    element_id      uuid REFERENCES ifc_element(id),
    corrects_id     uuid REFERENCES quantity(id),        -- manuelle Korrektur verweist auf Ursprung
    correction_reason text,
    quantity_type   text NOT NULL,                       -- z. B. Wandfläche netto
    value           numeric(18,4) NOT NULL,
    unit            text NOT NULL CHECK (unit IN ('mm','cm','m','m2','m3','lfm','stk','kg','t','h','psch')),
    rule_set        text,                                -- z. B. ÖNORM B 2204, VOB/C DIN 18330
    trade_id        uuid REFERENCES trade(id),
    storey_id       uuid REFERENCES storey(id),
    status          text NOT NULL DEFAULT 'draft' CHECK (status IN ('draft','checked','approved','transferred','void')),
    approved_by     uuid REFERENCES app_user(id),
    approved_at     timestamptz,
    created_by      uuid NOT NULL REFERENCES app_user(id),
    created_at      timestamptz NOT NULL DEFAULT now(),
    CHECK ((source = 'pdf_measure' AND markup_id IS NOT NULL)
        OR (source IN ('ifc_qto','ifc_geometry') AND element_id IS NOT NULL)
        OR (source = 'manual'))
);

CREATE TABLE boq_item_link (                            -- Verknüpfung zu LV-Position in EGLE360 AVA
    id              uuid PRIMARY KEY,
    project_id      uuid NOT NULL REFERENCES project(id) ON DELETE CASCADE,
    ava_boq_id      text NOT NULL,                       -- LV-Kennung in der AVA
    ava_item_number text NOT NULL,                       -- Positionsnummer (OZ)
    short_text      text,
    unit            text NOT NULL,
    unit_price      numeric(18,4),
    currency        char(3) NOT NULL DEFAULT 'EUR',
    UNIQUE (project_id, ava_boq_id, ava_item_number)
);

CREATE TABLE quantity_boq_assignment (
    quantity_id     uuid NOT NULL REFERENCES quantity(id) ON DELETE CASCADE,
    boq_item_link_id uuid NOT NULL REFERENCES boq_item_link(id) ON DELETE CASCADE,
    factor          numeric(12,6) NOT NULL DEFAULT 1,
    PRIMARY KEY (quantity_id, boq_item_link_id)
);

CREATE TABLE ava_transfer (
    id              uuid PRIMARY KEY,                    -- Übergabe-ID, idempotent auf AVA-Seite
    project_id      uuid NOT NULL REFERENCES project(id),
    payload_sha256  bytea NOT NULL,
    state           text NOT NULL CHECK (state IN ('prepared','sent','accepted','rejected')),
    response        jsonb,
    created_by      uuid NOT NULL REFERENCES app_user(id),
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE ava_transfer_item (
    transfer_id     uuid NOT NULL REFERENCES ava_transfer(id) ON DELETE CASCADE,
    quantity_id     uuid NOT NULL REFERENCES quantity(id),
    boq_item_link_id uuid NOT NULL REFERENCES boq_item_link(id),
    value           numeric(18,4) NOT NULL,
    PRIMARY KEY (transfer_id, quantity_id, boq_item_link_id)
);
-- Schutz gegen Doppelübernahme: eine Menge darf je LV-Position nur in einer angenommenen Übergabe stehen.
-- Wird per Trigger beim Wechsel auf state = 'accepted' geprüft (Implementierung in Phase 9).

CREATE TABLE cost_snapshot (
    id              uuid PRIMARY KEY,
    project_id      uuid NOT NULL REFERENCES project(id) ON DELETE CASCADE,
    label           text NOT NULL,
    model_version_ids uuid[],
    totals          jsonb NOT NULL,
    created_at      timestamptz NOT NULL DEFAULT now()
);

-- Termine (4D) ---------------------------------------------------------------

CREATE TABLE schedule_task (
    id              uuid PRIMARY KEY,
    project_id      uuid NOT NULL REFERENCES project(id) ON DELETE CASCADE,
    parent_id       uuid REFERENCES schedule_task(id),
    external_ref    text,
    name            text NOT NULL,
    planned_start   date,
    planned_finish  date,
    actual_start    date,
    actual_finish   date,
    progress_pct    numeric(5,2)
);

CREATE TABLE schedule_task_element (
    task_id         uuid NOT NULL REFERENCES schedule_task(id) ON DELETE CASCADE,
    global_id       char(22) NOT NULL,
    PRIMARY KEY (task_id, global_id)
);

-- Aufgaben, Mängel, BIM-Issues, BCF ----------------------------------------

CREATE TABLE issue (
    id              uuid PRIMARY KEY,
    project_id      uuid NOT NULL REFERENCES project(id) ON DELETE CASCADE,
    kind            text NOT NULL CHECK (kind IN ('task','defect','review_note','bim_issue','clash')),
    title           text NOT NULL,
    description     text,
    status          text NOT NULL DEFAULT 'open',
    priority        smallint,
    assignee_id     uuid REFERENCES app_user(id),
    due_date        date,
    markup_id       uuid REFERENCES markup(id),
    bcf_topic_guid  uuid UNIQUE,
    created_by      uuid NOT NULL REFERENCES app_user(id),
    created_at      timestamptz NOT NULL DEFAULT now(),
    closed_at       timestamptz
);
ALTER TABLE clash_result ADD CONSTRAINT clash_issue_fk FOREIGN KEY (issue_id) REFERENCES issue(id);

CREATE TABLE issue_element (
    issue_id        uuid NOT NULL REFERENCES issue(id) ON DELETE CASCADE,
    global_id       char(22) NOT NULL,
    PRIMARY KEY (issue_id, global_id)
);

CREATE TABLE issue_comment (
    id              uuid PRIMARY KEY,
    issue_id        uuid NOT NULL REFERENCES issue(id) ON DELETE CASCADE,
    author_id       uuid NOT NULL REFERENCES app_user(id),
    body            text NOT NULL,
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE issue_viewpoint (                          -- BCF-Viewpoint
    id              uuid PRIMARY KEY,
    issue_id        uuid NOT NULL REFERENCES issue(id) ON DELETE CASCADE,
    camera          jsonb NOT NULL,
    components      jsonb,
    snapshot_sha256 bytea REFERENCES stored_file(sha256)
);

CREATE TABLE issue_attachment (
    issue_id        uuid NOT NULL REFERENCES issue(id) ON DELETE CASCADE,
    file_sha256     bytea NOT NULL REFERENCES stored_file(sha256),
    file_name       text NOT NULL,
    PRIMARY KEY (issue_id, file_sha256)
);

-- KI-Prüfergebnisse, Berichte, Aufträge ------------------------------------

CREATE TABLE ai_finding (
    id              uuid PRIMARY KEY,
    project_id      uuid NOT NULL REFERENCES project(id) ON DELETE CASCADE,
    check_code      text NOT NULL,                      -- z. B. DOC_DUPLICATE_SHEET_NUMBER
    page_id         uuid REFERENCES page(id),
    element_id      uuid REFERENCES ifc_element(id),
    location        jsonb,                              -- Fundstelle (Koordinaten)
    message         text NOT NULL,
    confidence      numeric(4,3),
    is_uncertain    boolean NOT NULL,
    model_info      text NOT NULL,                      -- Modell/Regelversion
    status          text NOT NULL DEFAULT 'new',
    issue_id        uuid REFERENCES issue(id),
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE report (
    id              uuid PRIMARY KEY,
    project_id      uuid NOT NULL REFERENCES project(id) ON DELETE CASCADE,
    report_type     text NOT NULL,
    parameters      jsonb NOT NULL,
    file_sha256     bytea REFERENCES stored_file(sha256),
    created_by      uuid NOT NULL REFERENCES app_user(id),
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE job (                                      -- Warteschlange für Stapel-, OCR-, BIM-, KI-Aufträge
    id              uuid PRIMARY KEY,
    project_id      uuid REFERENCES project(id),
    job_type        text NOT NULL,
    payload         jsonb NOT NULL,
    state           text NOT NULL DEFAULT 'queued'
                    CHECK (state IN ('queued','running','succeeded','failed','cancelled')),
    attempts        int NOT NULL DEFAULT 0,
    log             jsonb NOT NULL DEFAULT '[]',
    created_by      uuid REFERENCES app_user(id),
    created_at      timestamptz NOT NULL DEFAULT now(),
    started_at      timestamptz,
    finished_at     timestamptz
);
CREATE INDEX job_queue_idx ON job(created_at) WHERE state = 'queued';

-- Änderungsprotokoll ---------------------------------------------------------

CREATE TABLE audit_log (
    id              bigserial PRIMARY KEY,
    occurred_at     timestamptz NOT NULL DEFAULT now(),
    user_id         uuid REFERENCES app_user(id),
    project_id      uuid REFERENCES project(id),
    action          text NOT NULL,
    entity_type     text NOT NULL,
    entity_id       uuid,
    details         jsonb,
    prev_hash       bytea,
    entry_hash      bytea NOT NULL                      -- SHA-256 über prev_hash + Inhalt
);
REVOKE UPDATE, DELETE ON audit_log FROM PUBLIC;
