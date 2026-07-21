using Leno.UserAuth.Domain.ValueObjects;

namespace Leno.UserAuth.Domain.Tests;

public class ValidationPatternTests
{
    #region UsernamePattern

    [Theory]
    [InlineData("testuser")]
    [InlineData("TestUser123")]
    [InlineData("abc")]
    [InlineData("a_b_c")]
    [InlineData("abcdefghijklmnopqrstuvwxyz012345")]
    [InlineData("ABC")]
    [InlineData("123")]
    [InlineData("_underscore_")]
    public void UsernamePattern_Should_Match_Valid_Usernames(string username)
    {
        UsernamePattern.GetRegex().IsMatch(username).Should().BeTrue();
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("abcdefghijklmnopqrstuvwxyz0123456")]
    [InlineData("invalid chars!")]
    [InlineData("user@name")]
    [InlineData("user.name")]
    [InlineData("user-name")]
    [InlineData("用户名")]
    [InlineData(" user ")]
    [InlineData("")]
    public void UsernamePattern_Should_Reject_Invalid_Usernames(string username)
    {
        UsernamePattern.GetRegex().IsMatch(username).Should().BeFalse();
    }

    [Fact]
    public void UsernamePattern_PatternStr_Should_Match_Regex()
    {
        // PatternStr 和 GetRegex() 应该匹配同一组输入
        var regex = UsernamePattern.GetRegex();
        regex.IsMatch("valid_user123").Should().BeTrue();
        System.Text.RegularExpressions.Regex.IsMatch("valid_user123", UsernamePattern.PatternStr).Should().BeTrue();
    }

    #endregion

    #region EmailPattern

    [Theory]
    [InlineData("test@example.com")]
    [InlineData("user.name@domain.org")]
    [InlineData("a@b.co")]
    [InlineData("TEST@EXAMPLE.COM")]
    [InlineData("user+tag@domain.com")]
    public void EmailPattern_Should_Match_Valid_Emails(string email)
    {
        EmailPattern.GetRegex().IsMatch(email).Should().BeTrue();
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("@example.com")]
    [InlineData("user@")]
    [InlineData("user@.com")]
    [InlineData("user@example")]
    [InlineData("user @example.com")]
    [InlineData("")]
    public void EmailPattern_Should_Reject_Invalid_Emails(string email)
    {
        EmailPattern.GetRegex().IsMatch(email).Should().BeFalse();
    }

    #endregion

    #region PhonePattern

    [Theory]
    [InlineData("+8613800138000")]
    [InlineData("+1234567890")]
    [InlineData("+112345678901234")]
    [InlineData("+1123")]
    public void PhonePattern_Should_Match_Valid_Phones(string phone)
    {
        PhonePattern.GetRegex().IsMatch(phone).Should().BeTrue();
    }

    [Theory]
    [InlineData("13800138000")]
    [InlineData("+0123456789")]
    [InlineData("008613800138000")]
    [InlineData("+abc")]
    [InlineData("+")]
    [InlineData("")]
    public void PhonePattern_Should_Reject_Invalid_Phones(string phone)
    {
        PhonePattern.GetRegex().IsMatch(phone).Should().BeFalse();
    }

    #endregion
}
