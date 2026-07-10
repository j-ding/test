using System.ComponentModel.DataAnnotations;

namespace SFSWebForm.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Username or email is required.")]
    [Display(Name = "Username or email")]
    public string Username { get; set; } = string.Empty;

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}
