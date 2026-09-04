using UMS.Modules.Student.Domain.Students;

namespace UMS.Modules.Student.UnitTests.Students;

public sealed class StudentNumberTests
{
    [Fact]
    public void FromIssuedSequence_formats_as_admissionYear_facultyCode_zeroPaddedSequence()
    {
        var studentNumber = StudentNumber.FromIssuedSequence(2026, "CSE", 7);

        Assert.Equal("2026CSE00007", studentNumber.Value);
    }

    [Fact]
    public void FromIssuedSequence_does_not_truncate_a_sequence_wider_than_the_padding_width()
    {
        var studentNumber = StudentNumber.FromIssuedSequence(2026, "CSE", 123456);

        Assert.Equal("2026CSE123456", studentNumber.Value);
    }

    [Theory]
    [InlineData("CSE", true)]
    [InlineData("A", true)]
    [InlineData("ABCDEFGHIJ", true)]
    [InlineData("cse", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("ABCDEFGHIJK", false)]
    [InlineData("CS-E", false)]
    public void IsValidFacultyCode_enforces_1_to_10_upper_case_alphanumeric(string? facultyCode, bool expectedValid)
    {
        Assert.Equal(expectedValid, StudentNumber.IsValidFacultyCode(facultyCode));
    }

    [Fact]
    public void FromIssuedSequence_throws_for_an_invalid_faculty_code()
    {
        Assert.Throws<ArgumentException>(() => StudentNumber.FromIssuedSequence(2026, "cse", 1));
    }
}
