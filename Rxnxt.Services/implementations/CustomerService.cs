using Rxnxt.Business.DTOs;
using Rxnxt.Business.Interfaces;
using Rxnxt.Domain.Models;

namespace Rxnxt.Services.Implementations;

public sealed class CustomerService
{
    private readonly ICustomerRepository _customerRepo;

    public CustomerService(ICustomerRepository customerRepo)
    {
        _customerRepo = customerRepo;
    }

    public async Task<List<CustomerSearchResult>> SearchAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2) return new List<CustomerSearchResult>();
        return await _customerRepo.SearchAsync(query.Trim());
    }

    public async Task<CustomerSearchResult?> GetByIdAsync(int id)
    {
        var customer = await _customerRepo.GetByIdAsync(id);
        if (customer == null) return null;

        return new CustomerSearchResult
        {
            Id = customer.Id,
            Name = customer.Name,
            Phone = customer.Phone,
            Email = customer.Email,
            LoyaltyPoints = customer.LoyaltyPoints,
            CustomerCode = customer.CustomerCode
        };
    }

    public async Task<(CustomerSearchResult? customer, string? error)> CreateAsync(string name, string phone)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(phone))
            return (null, "Name and phone are required");

        var normalizedPhone = phone.Trim();
        if (await _customerRepo.PhoneExistsAsync(normalizedPhone))
            return (null, "Phone number already exists");

        var created = await _customerRepo.CreateAsync(new Customer
        {
            Name = name.Trim(),
            Phone = normalizedPhone
        });

        return (new CustomerSearchResult
        {
            Id = created.Id,
            Name = created.Name,
            Phone = created.Phone,
            Email = created.Email,
            LoyaltyPoints = created.LoyaltyPoints
        }, null);
    }
}
