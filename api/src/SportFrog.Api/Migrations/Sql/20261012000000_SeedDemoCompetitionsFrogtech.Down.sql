-- =============================================================================
-- SeedDemoCompetitionsFrogtech (Down)
--
-- Removes the rows the Up script inserted, by id, children first. Rows that
-- were already there before Up ran are removed too if their ids match, which
-- is intended: the ids belong to this seed.
-- =============================================================================

DO $$
DECLARE
    v_org uuid;
BEGIN
    SELECT id INTO v_org FROM organizations WHERE slug = 'frogtech-solutions';

    IF v_org IS NULL THEN
        RETURN;
    END IF;

    PERFORM set_config('app.current_org', v_org::text, true);

    DELETE FROM player_events   WHERE id IN ('03496bbe-20e0-4d23-a511-328b983108c3', '17347aa9-7d4e-46cc-b86a-02fe8da0cb6a', '213af00e-d1a7-42dd-a571-75bd787b393a', '2e154305-cc74-469e-9bd3-ea3954f96d9b', '55e7c8aa-f4aa-4035-a0d0-615321a2be5f', '63d6397a-75e5-448e-8925-c613a74bc3ea', '69c46f0a-7393-4897-b10d-7abe1cafaebf', '73ac79cf-8016-418e-91d8-eecf521060ed', '74302547-326a-4924-814e-5f6e297eb350', '7e50b17b-b583-4a4a-bae0-5f9085491099', '7f4f0f1c-7cf0-473f-ac7a-9921d3414061', '7f7bb61d-d6be-4efd-9335-a239e07e47d5', '8076661e-9329-4d7a-888c-0d9913118a16', '88b8bf62-799a-4dc5-beca-381023fe1e5b', '8cf6245b-b752-49a7-85e0-6f48bdbfd843', '966c155f-235f-4808-94b0-f1365603f02e', '9a804fb0-70a1-4b4d-bc0c-f23e253f8125', 'a357db17-dec2-449b-a929-467d6b7aa39c', 'b2975583-fe9d-4905-8bd6-285944716224', 'ba9bd8ab-13ba-4927-bbee-a7ed4a371570', 'bed13f16-8bd3-43a3-8366-3014dff98c6c', 'bfee230a-c83d-42f0-92af-a1fab53f799b', 'c0f73ba7-f0b2-4cf3-a94f-e2ab211e0185', 'cbc595d8-bf24-408e-8535-18111b8b597f', 'cc233eb2-2aa3-46e9-8940-320242b92b67', 'cddfdb46-c00e-45e0-9ee6-05fde7858806', 'd48de779-da09-4419-8829-e0db031651f1', 'dce8334c-b106-4f89-94f3-902b52c87cb7', 'df46f28b-4dcf-4860-b79a-648668ce91ad', 'df81d948-19f6-46ed-a665-1e22773604b4', 'e4f4ee22-a9f1-4e26-8bb1-6abb743ed8b5', 'e6ac6923-7379-4b17-932b-4ffb904e37aa', 'ec10fe5f-1bb9-4680-bf08-a31ccea4f27f', 'ffe964b7-2736-4f26-9902-5e16d255e796');
    DELETE FROM matches         WHERE id IN ('01590dbf-35c1-486a-aa9c-6537d258c277', '121e2321-feb6-4c95-b5e6-113a6e84e6e7', '20fcd5a5-f8e2-4296-987d-b61d9d6d26b5', '2194839f-f366-40a7-8fc4-3a1f90cffd27', '28a97f3a-0219-4360-bf5c-8e8a9421e963', '3e5122d0-7010-4c9f-b12c-74e0b9d2b36e', '638df24c-70d4-4d85-9d9e-e88a461d9b05', '65a3d92b-f3fc-419b-8fbb-7407ca5a5bae', '6d9b1595-d626-4b73-91d6-0ae3ed67d70f', '6f83affd-5bf9-41c1-a85a-4affc5310859', '752b8f36-d09f-470c-ba50-c7a19fcad6d2', '7deab601-81d8-4ad6-8900-becb3b050815', '93d7ed4e-d92a-40c2-be0c-f13549032ce1', 'a006513a-bd44-4c91-8f2d-6166ffdd111e', 'a1417a30-ba5b-4778-a003-3d78eca2c6f5', 'b0af223a-bb5a-4878-89f9-03d8c5b03288', 'b9e28726-3a62-406b-8d21-3bf4cda54e8f', 'ba5712cc-7fdd-4c4c-8029-0794cb8e0093', 'dabaa73b-8a4f-470a-aad8-3cff23930a2a', 'e31802a8-fbfb-4ef8-962b-5d94e85f34b4', 'e57f9767-56a3-46d3-a245-11966a29f8cf', 'e8d6988c-4694-47ea-b1be-d4157ab4ef5d', 'eadf4e85-28d3-4f9b-83bb-e90a895ee24d', 'f0f74d22-e84b-4e9b-a4b0-9c9ffb0d04c6');
    DELETE FROM roster_entries  WHERE id IN ('0036ba21-62cf-4639-92b6-4f4e118b173e', '17a287d9-8990-4dfa-ae21-261f4eb08d6c', '1b7c6e38-f264-48a3-8603-d65e75563592', '3deabb86-5ffd-4c98-9bb0-92ab76b5d8b5', '40d11a23-d438-468b-a3b1-5623fe336cec', '5835274c-775a-4be3-a433-742b903ca97e', '74dad402-985d-4cab-bb7c-692efe27cd99', '93ad8831-b0be-43f3-8b78-c4dc61ca6520', 'd105ab81-0af3-45a4-87ff-ff933f23b9db', 'eb2a62e2-793c-4767-8e80-7b22972c95f9');
    DELETE FROM teams           WHERE id IN ('113c27d9-ab0d-42a1-a2d0-aa28b87f54a9', '1aeae65a-e08c-4201-a592-cd9c2364c0e3', '31c6b7b9-82a9-447c-a1c4-30e4ecccd628', '381174cf-e24d-436e-a953-5e9b246f9677', '38da0993-fbe4-4e88-aa6f-0594c427136a', '3d790a99-a2b9-44a8-92fb-8eb0d6fa55fe', '577ff51b-7336-4d6b-8ff4-c9d9e09e4e24', '691f8fd9-6461-4ad7-81aa-9e0587b4dd5a', '6bf73c61-83ed-4fff-8aaa-113b98c95e91', '6d76fa30-a7f5-458b-9720-07917fb59e7a', '850fe502-9414-41e6-a3c0-a1e12ffbf0e0', '8f8f0313-07c6-4501-b08b-3a7eacfe1789', '9fdd9465-331e-465b-9305-8cb3b8f55f16', 'aff77500-d045-49b7-bbe0-a1922fe4417e', 'b60caa45-afd6-41d9-ae38-4626f6ec08bb', 'c010b67d-094d-4621-be3a-b60102815a5e', 'c4cec12d-a546-47e3-9a6d-237d2d7c608d', 'efdeb935-d1b5-4745-b906-9e763ce4f24b');
    DELETE FROM categories      WHERE id IN ('413413f7-0265-40ff-9e60-67cfc4c3ed31', 'f0ccdf9a-a2fb-4746-8656-d73c57ea5608', 'f5225ebd-da95-4969-bed6-e94da8b0ec90');
    DELETE FROM competitions    WHERE id IN ('45708442-acb4-408d-8f2e-3a4e0d305a20', '97997415-7e0e-4705-89e0-ce9d597f3856', 'ece3af57-b4f6-465d-9460-41f8a567de6c');
    DELETE FROM rulesets        WHERE id IN ('0da86e7b-0841-4afb-9e4d-9a94fc1e9d28', 'b25a8dc3-1e04-4047-94ec-b15c22924708', 'b7434ba1-b647-4d96-8917-3cdaa71d9628');
    DELETE FROM athletes        WHERE id IN ('33a37070-bb1b-4d6c-9b1a-14cd3e834c6a', '4353ee3e-c837-4b8b-a7fe-062b1de78c87', '4d364c23-0a66-449f-b417-edc1f302f7fe', '5a6058d6-d276-4b39-99a2-83fea750bdab', 'a48e98f4-0337-46e8-9f35-a93b6599deb1', 'b865a36a-917d-4fe2-af8f-9f0fcb0903f9', 'e42b99db-2568-4cbb-8cda-3fbf77dadb2b', 'ea51566c-88dc-4cf1-ac81-5c7f70c0120c', 'f59575ce-c1fb-4271-a41a-34fac211f598', 'f895b8f1-c119-48d4-95b6-ca2c4ae7d31c');
    DELETE FROM clubs           WHERE id IN ('043712f9-423a-4c9a-8e5c-5c91dad29c48', '1cb5e78d-6f28-4583-be61-11c4f6c70149', '2ecb3f73-1f4f-4b97-8d23-464762e72fa3', '33f364c7-4648-44d4-aeed-ecfa0470f8de', '3c43ed35-1f29-4ac7-9610-222dce5d581f', '4b6b70ec-c0d0-443b-abc0-f4724e77fb4d', '522a0b80-c3e2-448a-8bf9-bffd1337c443', '7a13bb25-d428-4652-8c19-b6be028b6df4', '820ed9a9-7fdf-46f9-9a31-3a6fe9fe56a7', '8eb3c33a-5bbe-4b56-bea6-2ea9f6980936', 'c4ef5fce-1494-4ec7-9d4a-d46153c86356', 'cceff118-b3ed-4342-a5e3-73b7785f1ca2');
    DELETE FROM venue_spaces    WHERE id IN ('a27b5499-f471-438b-8a2c-205fb8f1fa8d');
    DELETE FROM venues          WHERE id IN ('577246a5-f019-41b8-905a-3f3fc21c321f');

    PERFORM set_config('app.current_org', '', true);
END
$$;
