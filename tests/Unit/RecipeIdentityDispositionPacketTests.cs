// SPDX-License-Identifier: GPL-3.0-or-later
using System.Reflection;
using System.Text.RegularExpressions;
using Foundry.Application;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.BuiltIn;
using Foundry.Modules.DeterministicPress;

namespace Foundry.Tests.Unit;

public sealed partial class RecipeIdentityDispositionPacketTests
{
    private static readonly string[] OutgoingRecipeIds =
    [
        "access-remix",
        "all-aboard.agency-cards",
        "all-aboard.first-then",
        "all-aboard.now-next-done",
        "all-aboard.task-strip",
        "board-to-brief",
        "directions-duet",
        "exit-lens",
        "family-bridge",
        "lesson-loom",
        "press.big-print",
        "press.blankforms",
        "press.booklet-guide",
        "press.flashcards",
        "press.foldables",
        "press.handwriting",
        "press.labels",
        "press.manipulatives",
        "rubric-relay",
        "scaffold-smith.packet",
        "scaffold-smith.task-entry",
        "source-lens",
        "talk-moves-studio",
    ];

    private static readonly string[] CandidateOnlyRecipeIds =
    [
        "press.calibration",
        "press.charts",
        "press.computational",
        "press.field-journal",
        "press.fluency",
        "press.glossary",
        "press.grouping",
        "press.history",
        "press.learner-held",
        "press.math-scaffolds",
        "press.protocols",
        "press.puzzles",
        "press.retrieval",
        "press.rubrics",
        "press.schedules",
    ];

    private static readonly Dictionary<string, string> OutgoingManifestSha256 =
        new(StringComparer.Ordinal)
        {
            ["access-remix"] = "162C5A2BE668A8340F2A850F1B2F304A1036332DD69B61836FD2E535F7BF04FD",
            ["all-aboard.agency-cards"] = "E21E4E21EA165EBBAD218B98AE9AB11F83F5AD82244AE77B872D27875CDBA21F",
            ["all-aboard.first-then"] = "78956CDFEE8D7D74701F545AAB5535BC9D2BCB69BA72C634A0F6197465D0B8E5",
            ["all-aboard.now-next-done"] = "48142E0FBBB86EEB9CAD1D9DC0172D696C047BA1E691D22D6BEC42DD60AC67AD",
            ["all-aboard.task-strip"] = "AB4F5222F52829BAAD8F79CE9163F95DBA56614B92A5FE50B7D139C7F0E42585",
            ["board-to-brief"] = "20873B30065DF05F62A4AF728D881F1D367312FEB3286A14D4DE4F7CB57C861A",
            ["directions-duet"] = "6DA01F0C7B8779420C7D76E737284119906FDEB409864F71CCC4D167E02A2929",
            ["exit-lens"] = "5BDEE8FB0A55BDDF1231408B5A17382D29B3F54419CBD0E8D6B6DFF136789359",
            ["family-bridge"] = "7F927525D384FC7798F83092E5CC6552D2BC25FA4E6E1170F6D362DF04B208E4",
            ["lesson-loom"] = "513448B0B369058F72BF1072B0EE47AC547B5433F1BD46B8629FCA136F26487D",
            ["press.big-print"] = "4CB47B3D5A39E29C6CFA2C02928D4E6CF8B16DBB77D4472549B2CEFC8B04050D",
            ["press.blankforms"] = "8807543DADC43916EC036FE79146D7E07B831BC8CB6841D57659CE4F8C7DAA81",
            ["press.booklet-guide"] = "01A03046EE2BCED7A87EA46CD152A735639EBD8C2465D831BD758F10F9E186D8",
            ["press.flashcards"] = "156ED6344D3BBA57425FEB4A98B27E59F86E1784EAEE5FAA600991D53A90EE84",
            ["press.foldables"] = "D9DE114FF77EBF3D855355EF5A048DDC82D7DF24B2FAA648E0B7BC405935C92D",
            ["press.handwriting"] = "273C5A162D814E5E3D3BF3227AB59C32A9261ECD477B828E63D7CD807721F08B",
            ["press.labels"] = "3C7AFB53CD7027AE9FA609F4E21D427C2B308843862776945CF83EF689D2EF1D",
            ["press.manipulatives"] = "212C9F559ECDF46D97501B1802226C583F68BFF9B295AC881D786F82D67E06E6",
            ["rubric-relay"] = "61BA6CBB6BED1D6A83F9452E9E81DB08A20CFD00FBC2FD9BA1A24823D50777D9",
            ["scaffold-smith.packet"] = "1CD8900489CF9AA78463328BEE31277521CF0A3736D382BDFF559FF90914F2BF",
            ["scaffold-smith.task-entry"] = "67D51E4082988D0B1E2F6ADFBCC4119A2C8DE3C22EBEB10D1BD197DDA3B7C42D",
            ["source-lens"] = "7CF1220A987AF7F2052AB1B489E79549E6683809B6B1F293A81B8E69347DA16E",
            ["talk-moves-studio"] = "826E14F062F180F403D7C07E78EE54EF6AF4E943F67523AB974AE54924DBFD76",
        };

    private static readonly Dictionary<string, string> CurrentManifestSha256 =
        new(StringComparer.Ordinal)
        {
            ["access-remix"] = "B657CC7D4DB7976E1F385D581A811975951CBA382D92AB6BB0994D4A5B55605C",
            ["all-aboard.agency-cards"] = "E0C3D180512DE80D04166A3AA0BFA44352BF77C946B90F88C512B8C534553536",
            ["all-aboard.first-then"] = "C1450269643BA899679A577E1789ED717B6F03D9BBBED1DE97997732FB4548C6",
            ["all-aboard.now-next-done"] = "16995ADD60752602CC6A6C5627754FCFCD4E47754868FB323E19FA1D671F8AFD",
            ["all-aboard.task-strip"] = "080F0411281588B5A44994502DD4200582DCC68726274A1AFE61D03AF254A520",
            ["board-to-brief"] = "20873B30065DF05F62A4AF728D881F1D367312FEB3286A14D4DE4F7CB57C861A",
            ["directions-duet"] = "C7F07EAE06055D3A1DBEC49F4A2E4A04F345DE7E7CF22621D3DA60F3B87FD7CC",
            ["exit-lens"] = "5BDEE8FB0A55BDDF1231408B5A17382D29B3F54419CBD0E8D6B6DFF136789359",
            ["family-bridge"] = "E412A7A9D9DE717533CC1280F311513A3D7DB04D00A2A0BCDDBB1BC031E62BE5",
            ["lesson-loom"] = "E3B1E39BC8069E4F6DA2A5C8605DE63B9FA217FD89D540EFFAA122B3811ECE1F",
            ["press.big-print"] = "4CB47B3D5A39E29C6CFA2C02928D4E6CF8B16DBB77D4472549B2CEFC8B04050D",
            ["press.blankforms"] = "419CF59E3227C0A57B9302AD47F603B6905C76F13ABAFD4A865DE250DAC79177",
            ["press.booklet-guide"] = "01A03046EE2BCED7A87EA46CD152A735639EBD8C2465D831BD758F10F9E186D8",
            ["press.flashcards"] = "156ED6344D3BBA57425FEB4A98B27E59F86E1784EAEE5FAA600991D53A90EE84",
            ["press.foldables"] = "D9DE114FF77EBF3D855355EF5A048DDC82D7DF24B2FAA648E0B7BC405935C92D",
            ["press.handwriting"] = "273C5A162D814E5E3D3BF3227AB59C32A9261ECD477B828E63D7CD807721F08B",
            ["press.labels"] = "3C7AFB53CD7027AE9FA609F4E21D427C2B308843862776945CF83EF689D2EF1D",
            ["press.manipulatives"] = "913580FE097A4735955D02BCF6AD2F5562E83A158B23221F6E635F2AE82DF97E",
            ["rubric-relay"] = "61BA6CBB6BED1D6A83F9452E9E81DB08A20CFD00FBC2FD9BA1A24823D50777D9",
            ["scaffold-smith.packet"] = "1CD8900489CF9AA78463328BEE31277521CF0A3736D382BDFF559FF90914F2BF",
            ["scaffold-smith.task-entry"] = "22D63BCC3CB78F982091452E1A2E13FAC7B6FF04FF544E94E09202F2804FEC02",
            ["source-lens"] = "7CF1220A987AF7F2052AB1B489E79549E6683809B6B1F293A81B8E69347DA16E",
            ["talk-moves-studio"] = "826E14F062F180F403D7C07E78EE54EF6AF4E943F67523AB974AE54924DBFD76",
        };

    private static readonly Dictionary<string, string> CandidateManifestSha256 =
        new(StringComparer.Ordinal)
        {
            ["access-remix"] = "FA99DC89BA4AD6D220B099643D2C6121383FE15477ECAA50775EE56ADC10F74C",
            ["all-aboard.agency-cards"] = "FDE8EB525834B2AA424CF14D372ADE58A4A3D3498095B025C8B11B4186812454",
            ["all-aboard.first-then"] = "BE9B04C48002EE77DE197837C1DE1B6A4FC10EBAFEF9907A5D3F0AA029080AD8",
            ["all-aboard.now-next-done"] = "6DA2D2CF5171C609EC49A9B97622F9B9899F4FC74D930D42E0560C03D7711A46",
            ["all-aboard.task-strip"] = "B37386D3E562E3E2F4CD5FC05065F9CBD41529AAAA70951D84AC26E03DB7F094",
            ["board-to-brief"] = "A1E6B588E55021D60E1A9FFF1FBBE5CF60F8A8BFC7985B071E7D8B5FC63BB471",
            ["directions-duet"] = "CF86EDBB46B9DFBD7991E255B8920EFAEC9ACD1E608B27B71B5C4914DBBF7F5C",
            ["exit-lens"] = "76054CDCA21C7CB0D6159A27ADB06180ECFD3BA6B7BC17A51A2FD9125DD9C82B",
            ["family-bridge"] = "A7A8A9E81C4ECD931A3B6B929E65B3059FCDEB28E2819C3EB784B8B9919BFA3B",
            ["lesson-loom"] = "6644849B3EA513821EF81737DD87047DC8FF8DA816B55B69E69B9CD8C4D00289",
            ["press.big-print"] = "F8786CEC1C0A48A382AA88D9E3CD479D6395C7110365C672963402A4B4FFC9F2",
            ["press.blankforms"] = "A4EA79276F62355419D11D2FB08CEE8EDC386680E8B002A9A4F6D3E7449A94D7",
            ["press.booklet-guide"] = "1DC219879D76C50128AF43E344398A3B52C25C30088B92D5D8E40405EF25EF51",
            ["press.calibration"] = "D4EF35243CC43AB847A3FF7443826299968501795D44F6BC30E1007D53F8773E",
            ["press.charts"] = "E5937D4D7B8494FF081A1E7657EBFABF5049952BE9DD6AF166FEB3D1A33B3518",
            ["press.computational"] = "D70950F6118EAD681D2565B0A45371919F72B4CDE4A925362607079735EAADE2",
            ["press.field-journal"] = "9CC177EF0D3DE8E2C9BCCA500F5CCEE736E3557BBBB0AE0F15A6AFA7C1711C59",
            ["press.flashcards"] = "18B4B7167D38FD941B65B1EC19D5E0C24F397974B9AC19AEC070ED4F54F9F5CF",
            ["press.fluency"] = "065DEA8D80616EEB63DBA935EDD36568416D50CFBC45181D210E1E004235860E",
            ["press.foldables"] = "55A3E24663DA5051133292541F15439C33ED48C44F9B8E5254663A8661EAB861",
            ["press.glossary"] = "B57D198C81DDE52B3759494FD5797E3059E84CE423A584BD7C8FA3DFB0996751",
            ["press.grouping"] = "8856C92FB62BBEB0496B3FDA22E344E3EDAC791733ABBFAD3C4130D72BEC7FAC",
            ["press.handwriting"] = "85AED146613D967605ADE75AEC64E62FE4EC00B20BA37847942B262FFD0FC77C",
            ["press.history"] = "33944CC852F4009FD0D9027B09D49D3173D8C5E2CD7C60AB696BB202912B4BE0",
            ["press.labels"] = "CF161F36F77394733896F6712873DCA727B59B5B7BD32B84F26014F7372F4D41",
            ["press.learner-held"] = "363B309D85421A517A8530883A3AC63829EF3CBD9B6D1CE1450DBCA7966EA801",
            ["press.manipulatives"] = "C08B6CD5056A48046001F232166F31E9E8C9156045672F1C6BA4DFF78FE1D3C3",
            ["press.math-scaffolds"] = "58792158AE76F7349CBEF6B7A639607135440E60DC3F9B6B0F1C213428F8DD8E",
            ["press.protocols"] = "068A87AE3D53CFF07EA0472CE6E7F9CF6CED97C0CB2C8F664BCF21C024293D2F",
            ["press.puzzles"] = "1B1313D13C89D6CCD803B33D4AA3F3243B8BC49C1043DFC22BC16A6E48DE5693",
            ["press.retrieval"] = "4B7CABB7003B9BEE2C21388E216F7126841AE673134DF83EDAB1C09839517A9C",
            ["press.rubrics"] = "2803F649DE3CB59E1ECFECAAEFEE84B9CC37C54DD2C63C7730B66B7CADAECAC5",
            ["press.schedules"] = "D1FED11C73659BE5936B2EFA74D126E09ACD2AE8D7F6B2F0DDDFD5E4344BBF71",
            ["rubric-relay"] = "9ACEF556796C706905F36F4225CF008280C1C1206FAA1B58E69C42996D14C17F",
            ["scaffold-smith.packet"] = "AA4C6F6AC1440BE0E919DA95C3920AF1B7198F9EE296F05EAB97DAA22B10E5B6",
            ["scaffold-smith.task-entry"] = "1F433EC97A18BAC22BE35EEE694C72A9C135BBFF77A6FAB2B97DB5C2EBA027FD",
            ["source-lens"] = "5A96F3ABA5531C17A0EE4B61525DA640A9DB6B5E0BF542E851EEF8FEA78CB557",
            ["talk-moves-studio"] = "CAC76D811BF1F539B1AD3D5E6CA5D71891ECBBD746AEB7212F552B3FFF563297",
        };

    private static readonly Dictionary<string, string> CandidateOnlyDefaultOutputSha256 =
        new(StringComparer.Ordinal)
        {
            ["algorithm-cards"] = "34205fccc5c59a0d61bef9623fae3f9bb8213d641c9c43f41923f0b0ed71e7ae",
            ["bar-chart"] = "fad40ab0c45688e912b758efbfebda55d9a3b4a7e34f85102d93a2bd6a8a4256",
            ["bell-to-bell"] = "08fbb68f33e06fd2b23c377cd4b77ecbdc1e119e5baf4c675169c4bd72d8e4da",
            ["bingo-cards"] = "ae76e15e41f27d73fb6baf3d12ec0b69adcd3aa6b367ca99478acdfba114782d",
            ["bug-zoo"] = "81c867047d7688ee97d8c6a6de11c0113101459fd665b45d160cf768333a4930",
            ["calibration-proof"] = "ca4f89861601544e74780e8dc62364318c3bf83c6fa3463d55ac8f147e00784a",
            ["concept-sort"] = "a3598a33ab272076b5e0f467508330d58edeb838d66a5fa6d2094f97492fe3f1",
            ["done-definition"] = "62a511429540c97e90a5fa2ae4836ce0b706f0a0a2d28f297d29ade86a276449",
            ["estimation-first"] = "518144ea7829a526cdebf5602cf927333d4b7761bc7cdb50dd1c25715a082fb7",
            ["field-log"] = "1f7ad24e2c7a1c5de1fb661bf420652cd87ed3b335376c2ab6ab07d039338b4e",
            ["fluency-rehearsal"] = "a22c2f8aa6c4ff7ee9e8e2c020e32f640ca9f2b71b38252ac949037eecf7adbb",
            ["glossary-garden"] = "bfae9e9748e6df49191d551de295f87a4fe632f344b58d9b415731fc8429786c",
            ["goal-post"] = "bfc0066a344e173cf686b00cca699eda471c1d4efa20c1bba09e28f2ff70fa86",
            ["grouping-cards"] = "e654da5681ed8991f3a2693d77bbcb10a5563e9db49238cd503e89635c826a5b",
            ["observation-frame"] = "9a906ce81a38f6ccd6cf601bf1662b4ca313195adbc931bf3888b300660dd090",
            ["one-point-rubric"] = "d9adad530af47598dce7ed805561f002003c2821c3beb08f8358f3c034a98395",
            ["parsons-puzzle"] = "207abda1cd769b2a88a558ea515f32781fcb9aaf8bea16cd86ad489a5f03eceb",
            ["peer-feedback"] = "062251715688bb6fa984f2030ed2b2a55cdb2243b3a107a36bb54ef61380f130",
            ["portfolio-passport"] = "b9732c607f107a64a8e87dfd83603f9383fb54aa3a1c9123d8e4ccead8dc4ec2",
            ["retrieval-grids"] = "952546c181b2f57c4a87b7a6b3068fec50bbebb560398bc183137f034d17b418",
            ["role-cards"] = "59976365a6080ffcc164b9298f85038390e46ee625624b54df1b5cd975bcbd3b",
            ["rubber-duck-deck"] = "0c0adf9586fbb45044da72d3723d0ce76b83121cc48a24d2076445350ee55cf5",
            ["site-map"] = "425dec1a59439045e9b868c01558d9978f22375e3405433ba8e06bd498ddfc64",
            ["specimen-labels"] = "b0866cc8970e93aa1d284a16afca5e40d7564102acfaa3b3953fe5637fe6359a",
            ["strategy-shelf"] = "9b68a14947f1514a5d42d47b96e23a9097fb7d3d5433bd328de78b8f3e36b362",
            ["success-criteria"] = "b9da4dbfb9267185a275ed42b75fa29dc2ddef29172c6a182568662ba6a1cf81",
            ["synthesis-table"] = "bdb8f132f9a11c3414ea12b74ffa7ae637ac5c82e0971138bdcec652bc9dfcf3",
            ["timeline"] = "5b86c64d77043b02737ee05aabf536fd40f776083b8feb001bdd7f45f1990c86",
            ["trace-table"] = "dbeb6cc4d1beebedf77e781c844038d3fce64cf873eb6fa0d1acec094a7e1f5e",
            ["word-search"] = "42f4a775e88fd343d2253f15089e3eb301e8d1e773533b2739be91dec24ddd10",
            ["worked-example-fader"] = "d94d690b128a03d918fe5b42ed16e0ae02b045886238531873a6941224e80fb2",
        };

    private static readonly Dictionary<string, string> DirectManifestDeltaFields =
        new(StringComparer.Ordinal)
        {
            ["access-remix"] = "Warnings",
            ["all-aboard.agency-cards"] = "SupportedExports",
            ["all-aboard.first-then"] = "SupportedExports",
            ["all-aboard.now-next-done"] = "SupportedExports",
            ["all-aboard.task-strip"] = "SupportedExports",
            ["directions-duet"] = "ProhibitedPurposes, AllowedInputKinds, Warnings",
            ["family-bridge"] = "ProhibitedPurposes, AllowedInputKinds, Warnings",
            ["lesson-loom"] = "InstructionalPurpose",
            ["press.blankforms"] = "InstructionalPurpose",
            ["press.manipulatives"] = "InstructionalPurpose",
            ["scaffold-smith.task-entry"] = "InstructionalPurpose",
        };

    private static readonly string[] DirectManifestDeltaRecipeIds =
    [
        "access-remix",
        "all-aboard.agency-cards",
        "all-aboard.first-then",
        "all-aboard.now-next-done",
        "all-aboard.task-strip",
        "directions-duet",
        "family-bridge",
        "lesson-loom",
        "press.blankforms",
        "press.manipulatives",
        "scaffold-smith.task-entry",
    ];

    [Fact]
    public void Every_manifest_warning_is_a_fresh_required_confirmation_in_the_shared_review_validator()
    {
        foreach (var recipe in DiscoverDeclaredRecipeManifests())
        {
            var first = ReviewNoticeValidator.RequiredRecipeWarnings(recipe);
            var second = ReviewNoticeValidator.RequiredRecipeWarnings(recipe);

            Assert.Equal(recipe.Warnings.Count, first.Count);
            Assert.Equal(first, second);
            for (var index = 0; index < recipe.Warnings.Count; index++)
            {
                Assert.Equal($"recipe.warning.{index + 1}", first[index].Code);
                Assert.Equal(recipe.Warnings[index], first[index].Message);
                Assert.Equal(ValidationSeverity.Warning, first[index].Severity);
                Assert.True(first[index].RequiresAcknowledgement);
                Assert.NotSame(first[index], second[index]);
            }
        }
    }

    [Fact]
    public void Transitional_option_a_packet_exhaustively_freezes_all_recipe_contracts()
    {
        var packet = PacketText();

        Assert.Contains(
            "**Status:** DECIDED — OPTION A; candidate freeze hash pending in local C1; do not push this transitional state",
            packet,
            StringComparison.Ordinal);
        Assert.Contains("2026-09-01T07:55:28.9491461Z", packet, StringComparison.Ordinal);
        Assert.Contains(
            "Ratify Option A for all 23 outgoing recipe rows",
            packet,
            StringComparison.Ordinal);
        Assert.Contains("PENDING-C1-COMMIT-HASH", packet, StringComparison.Ordinal);
        Assert.Contains("record-only C2", packet, StringComparison.Ordinal);
        Assert.Contains("Still Proposed", packet, StringComparison.Ordinal);
        Assert.Contains("separately authorized ADR defines schema 2", packet, StringComparison.Ordinal);
        Assert.Contains(
            "a64abead04e56085b82ac632180ca1a362eb8bc3",
            packet,
            StringComparison.Ordinal);
        Assert.Contains(
            "380a0e5c3b768bdaa655825b35a25307fe89c0e5",
            packet,
            StringComparison.Ordinal);
        Assert.Contains("11 of the 23 outgoing identities", packet, StringComparison.Ordinal);
        Assert.Contains(RecipeContractFingerprint.FramingVersion, packet, StringComparison.Ordinal);
        Assert.Contains("local preprocessing", packet, StringComparison.Ordinal);
        Assert.Contains("localization resources", packet, StringComparison.Ordinal);
        Assert.Contains("migration IDs", packet, StringComparison.Ordinal);
        Assert.DoesNotContain("[A/B/U]", packet, StringComparison.Ordinal);
        Assert.DoesNotContain("[exact]", packet, StringComparison.Ordinal);

        var currentManifests = DiscoverDeclaredRecipeManifests();
        var expectedCurrent = OutgoingRecipeIds
            .Concat(CandidateOnlyRecipeIds)
            .Select(recipeId => $"{recipeId}@0.1.0")
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            expectedCurrent,
            currentManifests.Select(manifest => $"{manifest.Id}@{manifest.Version}"));
        Assert.Equal(expectedCurrent.Length, CandidateManifestSha256.Count);

        var currentById = currentManifests.ToDictionary(manifest => manifest.Id, StringComparer.Ordinal);
        foreach (var (recipeId, expectedSha256) in CandidateManifestSha256)
        {
            Assert.Equal(expectedSha256, RecipeContractFingerprint.ComputeSha256(currentById[recipeId]));
            Assert.Empty(currentById[recipeId].LocalPreprocessingIds);
            Assert.Empty(currentById[recipeId].LocalizationResourceIds);
            Assert.Empty(currentById[recipeId].MigrationIds);
        }

        var dispositionRows = DispositionRow().Matches(packet).ToArray();
        Assert.Equal(OutgoingRecipeIds, dispositionRows.Select(match => match.Groups["id"].Value));
        foreach (var row in dispositionRows)
        {
            var recipeId = row.Groups["id"].Value;
            var manifest = currentById[recipeId];
            Assert.Equal(OutgoingManifestSha256[recipeId], row.Groups["outgoing"].Value);
            Assert.Equal(CandidateManifestSha256[recipeId], row.Groups["candidate"].Value);
            Assert.Equal(manifest.Version, row.Groups["version"].Value);
            Assert.Equal(manifest.OutputSchemaId, row.Groups["schema"].Value);
            Assert.Equal(manifest.EvaluationSuiteVersion, row.Groups["evaluation"].Value);
        }

        var candidateRows = CandidateOnlyRow().Matches(packet).ToArray();
        Assert.Equal(CandidateOnlyRecipeIds, candidateRows.Select(match => match.Groups["id"].Value));
        foreach (var row in candidateRows)
        {
            var recipeId = row.Groups["id"].Value;
            var manifest = currentById[recipeId];
            Assert.Equal(CandidateManifestSha256[recipeId], row.Groups["candidate"].Value);
            Assert.Equal(manifest.Version, row.Groups["version"].Value);
            Assert.Equal(manifest.OutputSchemaId, row.Groups["schema"].Value);
            Assert.Equal(manifest.EvaluationSuiteVersion, row.Groups["evaluation"].Value);

            var recordedDefinitions = BacktickedDefinitionId().Matches(row.Groups["definitions"].Value)
                .Select(match => match.Groups["id"].Value)
                .Order(StringComparer.Ordinal)
                .ToArray();
            var executingDefinitions = PressRoomCatalog.All
                .Where(definition => string.Equals(definition.Recipe.Id, recipeId, StringComparison.Ordinal))
                .Select(definition => definition.Id)
                .Order(StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(executingDefinitions, recordedDefinitions);
        }

        AssertHistoricalV1Evidence(packet);
    }

    [Fact]
    public void Candidate_only_default_semantic_outputs_are_exactly_frozen()
    {
        var candidateOnly = CandidateOnlyRecipeIds.ToHashSet(StringComparer.Ordinal);
        var definitions = PressRoomCatalog.All
            .Where(definition => candidateOnly.Contains(definition.Recipe.Id))
            .OrderBy(definition => definition.Id, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            CandidateOnlyDefaultOutputSha256.Keys.Order(StringComparer.Ordinal),
            definitions.Select(definition => definition.Id));
        foreach (var definition in definitions)
        {
            var document = definition.Build(new PressInputs(PressRoomCatalog.Defaults(definition)));
            Assert.Equal(
                CandidateOnlyDefaultOutputSha256[definition.Id],
                ArtifactDocumentFingerprint.Compute(document));
        }
    }

    [Fact]
    public void Rendered_first_admission_sample_manifest_is_framed_and_pinned()
    {
        var bytes = File.ReadAllBytes(Path.Combine(
            RepoRoot(),
            "tests",
            "Rendering",
            "Fixtures",
            "recipe-first-admission-samples.sha256"));

        Assert.DoesNotContain((byte)'\r', bytes);
        Assert.Equal(
            "DEF10A3258A2F2ABA922DF8F1BC38FC3A3209065B36F81F44C41B4FE047F4A90",
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)));

        var lines = System.Text.Encoding.UTF8.GetString(bytes)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(40, lines.Length);
        Assert.Equal(lines.Order(StringComparer.Ordinal), lines);
        Assert.Equal(lines.Length, lines.Distinct(StringComparer.Ordinal).Count());
        Assert.All(lines, line => Assert.Matches(@"^[^ ]+ [0-9A-F]{64}$", line));
    }

    private static string PacketText()
        => File.ReadAllText(Path.Combine(
            RepoRoot(),
            "docs",
            "adr",
            "recipe-identity-disposition-packet.md"));

    private static void AssertHistoricalV1Evidence(string packet)
    {
        Assert.Equal(OutgoingRecipeIds, OutgoingManifestSha256.Keys);
        Assert.Equal(OutgoingRecipeIds, CurrentManifestSha256.Keys);

        var evidenceRows = ManifestEvidenceRow().Matches(packet)
            .ToDictionary(
                match => match.Groups["id"].Value,
                match => match.Groups["delta"].Value.Trim(),
                StringComparer.Ordinal);
        Assert.Equal(OutgoingRecipeIds, evidenceRows.Keys);
        foreach (var recipeId in OutgoingRecipeIds)
        {
            var expectedDelta = DirectManifestDeltaFields.TryGetValue(recipeId, out var fields)
                ? fields
                : "None directly measured";
            Assert.Equal(expectedDelta, evidenceRows[recipeId].Replace("`", string.Empty));
        }

        var fingerprintEvidenceRows = FingerprintEvidenceRow().Matches(packet)
            .ToDictionary(
                match => match.Groups["id"].Value,
                match => (
                    Outgoing: match.Groups["outgoing"].Value,
                    Current: match.Groups["current"].Value),
                StringComparer.Ordinal);
        Assert.Equal(OutgoingRecipeIds, fingerprintEvidenceRows.Keys);
        foreach (var recipeId in OutgoingRecipeIds)
        {
            Assert.Equal(OutgoingManifestSha256[recipeId], fingerprintEvidenceRows[recipeId].Outgoing);
            Assert.Equal(CurrentManifestSha256[recipeId], fingerprintEvidenceRows[recipeId].Current);
        }

        var measuredChanges = CurrentManifestSha256
            .Where(pair => !string.Equals(
                pair.Value,
                OutgoingManifestSha256[pair.Key],
                StringComparison.Ordinal))
            .Select(pair => pair.Key)
            .Order(StringComparer.Ordinal);
        Assert.Equal(DirectManifestDeltaRecipeIds, measuredChanges);
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "OpenClassroomFoundry.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Repository root not found.");
    }

    private static IReadOnlyList<RecipeManifest> DiscoverDeclaredRecipeManifests()
    {
        var manifests = new List<RecipeManifest>();
        var assemblies = new[]
        {
            typeof(ModuleStudioCatalog).Assembly,
            typeof(DeterministicPressRecipes).Assembly,
        };

        foreach (var type in assemblies.SelectMany(assembly => assembly.GetTypes()))
        {
            foreach (var property in type.GetProperties(
                         BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (IsManifestCarrier(property.PropertyType))
                {
                    Collect(property.PropertyType, property.GetValue(null));
                }
            }

            foreach (var field in type.GetFields(
                         BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (IsManifestCarrier(field.FieldType))
                {
                    Collect(field.FieldType, field.GetValue(null));
                }
            }
        }

        return [.. manifests
            .GroupBy(manifest => $"{manifest.Id}@{manifest.Version}", StringComparer.Ordinal)
            .Select(group =>
            {
                Assert.Single(group
                    .Select(RecipeContractFingerprint.ComputeSha256)
                    .Distinct(StringComparer.Ordinal));
                return group.First();
            })
            .OrderBy(manifest => manifest.Id, StringComparer.Ordinal)
            .ThenBy(manifest => manifest.Version, StringComparer.Ordinal)];

        void Collect(Type declaredType, object? value)
        {
            if (declaredType == typeof(RecipeManifest) && value is RecipeManifest manifest)
            {
                manifests.Add(manifest);
            }
            else if (typeof(IEnumerable<RecipeManifest>).IsAssignableFrom(declaredType)
                     && value is IEnumerable<RecipeManifest> sequence)
            {
                manifests.AddRange(sequence);
            }
        }

        static bool IsManifestCarrier(Type declaredType)
        {
            return declaredType == typeof(RecipeManifest)
                        || typeof(IEnumerable<RecipeManifest>).IsAssignableFrom(declaredType);
        }
    }

    [GeneratedRegex(
        @"^\| `(?<id>[^`]+)` \| `A` \| `(?<outgoing>[0-9A-F]{64})` \| `(?<candidate>[0-9A-F]{64})` \| `(?<version>[^`]+)` \| `(?<schema>[^`]+)` \| `(?<evaluation>[^`]+)` \|",
        RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex DispositionRow();

    [GeneratedRegex(
        @"^\| `(?<id>press\.[^`]+)` \| `(?<candidate>[0-9A-F]{64})` \| `(?<version>[^`]+)` \| `(?<schema>[^`]+)` \| `(?<evaluation>[^`]+)` \| (?<definitions>[^|]+) \|$",
        RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex CandidateOnlyRow();

    [GeneratedRegex(
        @"^\| `(?<id>[^`]+)` \| `0\.1\.0` \| (?<delta>[^|]+?) \|",
        RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex ManifestEvidenceRow();

    [GeneratedRegex(
        @"^\| `(?<id>[^`]+)` \| `(?<outgoing>[0-9A-F]{64})` \| `(?<current>[0-9A-F]{64})` \|$",
        RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex FingerprintEvidenceRow();

    [GeneratedRegex(@"`(?<id>[a-z0-9-]+)`", RegexOptions.CultureInvariant)]
    private static partial Regex BacktickedDefinitionId();
}
